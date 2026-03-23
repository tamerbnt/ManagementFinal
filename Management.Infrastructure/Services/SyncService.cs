using System;
using System.Linq;
using System.Reflection; // Added for JIT Repair Logic
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Infrastructure.Data;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Infrastructure.Data.Models;
using Management.Domain.Enums;
using Management.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Management.Domain.Services; // Added for ITenantService
using Newtonsoft.Json.Linq;
using Supabase.Realtime;
using System.Diagnostics;
using Management.Infrastructure.Integrations.Supabase.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Gotrue;
using Management.Infrastructure.Services.Sync;

namespace Management.Infrastructure.Services
{
    public class SyncService : ISyncService
    {
        private readonly Supabase.Client _supabase;
        private readonly ISecureStorageService _secureStorage;
        private readonly ILogger<SyncService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantService _tenantService;
        private readonly IToastService _toastService;
        private readonly Management.Domain.Services.ISessionStorageService _sessionStorage;
        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
        private const string LAST_SYNC_KEY = "LastSyncTimestamp";
        private readonly IFacilityContextService _facilityContext;
        private readonly IEnumerable<IFacilitySyncStrategy> _strategies;

        // JIT Context for repairing poisoned outbox messages
        private Guid? _currentFacilityId;

        // Phase 3: Session Circuit Breaker
        private int _sessionFailureCount = 0;
        private DateTime _lastSessionFailure = DateTime.MinValue;
        private const int MaxSessionFailures = 5;
        private static readonly TimeSpan SessionCooldown = TimeSpan.FromMinutes(5);

        public SyncStatus Status { get; private set; } = SyncStatus.Idle;
        public event EventHandler<SyncStatus>? SyncStatusChanged;
        public event EventHandler? SyncCompleted;

        public SyncService(
            Supabase.Client supabase, 
            ISecureStorageService secureStorage, 
            ILogger<SyncService> logger, 
            IServiceScopeFactory scopeFactory, 
            ITenantService tenantService, 
            Management.Domain.Services.ISessionStorageService sessionStorage,
            IToastService toastService,
            IFacilityContextService facilityContext,
            IEnumerable<IFacilitySyncStrategy> strategies)
        {
            _supabase = supabase;
            _secureStorage = secureStorage;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _tenantService = tenantService;
            _sessionStorage = sessionStorage;
            _toastService = toastService;
            _facilityContext = facilityContext;
            _strategies = strategies;
        }

        private void UpdateStatus(SyncStatus newStatus)
        {
            Status = newStatus;
            SyncStatusChanged?.Invoke(this, newStatus);
        }

        public async Task<int> GetPendingOutboxCountAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await context.OutboxMessages.CountAsync();
            }
        }

        public async Task<bool> WaitForPendingSyncAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Sync] Starting Sync Drain (waiting for outbox to clear)...");
            
            while (!ct.IsCancellationRequested)
            {
                int count = await GetPendingOutboxCountAsync();
                if (count == 0)
                {
                    _logger.LogInformation("[Sync] Sync Drain complete. Outbox is empty.");
                    return true;
                }

                _logger.LogDebug("[Sync] Drain in progress: {Count} items remaining...", count);
                
                // Trigger a push if not already syncing
                if (Status != SyncStatus.Syncing)
                {
                    _ = PushChangesAsync(ct);
                }

                try { await Task.Delay(500, ct); } catch { break; }
            }

            return await GetPendingOutboxCountAsync() == 0;
        }

        public async Task<bool> PushChangesAsync(CancellationToken ct, Guid? facilityId = null)
        {
            // Phase 1: Try to restore session from storage if not already active
            if (_supabase.Auth.CurrentSession == null)
            {
                var restored = await TryRestoreSupabaseSessionAsync();
                if (!restored)
                {
                    _logger.LogWarning("Sync: No active Supabase session and unable to restore from storage. Skipping Push. (User may be in offline mode)");
                    _toastService.ShowWarning("Sync skipped: No active cloud session. Changes saved locally.", "Cloud Offline");
                    UpdateStatus(SyncStatus.Offline);
                    return false;
                }
            }

            if (!await _syncSemaphore.WaitAsync(0, ct)) return false;

            try
            {
                UpdateStatus(SyncStatus.Syncing);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Set current facility context for JIT repair
                _currentFacilityId = facilityId;

                // Phase 2: Use the Outbox Pattern
                await ProcessOutboxAsync(context, ct, facilityId);
                
                UpdateStatus(SyncStatus.Idle);
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus(SyncStatus.Error);
                _logger.LogError(ex, "Sync Push Critical Error");
                _toastService.ShowError($"Sync failed: {ex.Message}", "Database Sync Error");
                return false;
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        private async Task ProcessOutboxAsync(AppDbContext context, CancellationToken ct, Guid? facilityId = null)
        {
            const int BatchSize = 25;
            
            // If facilityId is provided, we ONLY process messages for that facility.
            // If null, we fall back to current context (legacy) or all (global admin).
            var delayThreshold = DateTime.UtcNow.AddSeconds(-7);
            var query = context.OutboxMessages
                .IgnoreQueryFilters()
                .Where(m => !m.IsProcessed && !m.IsDeadLetter && m.ErrorCount < 5 && m.CreatedAt < delayThreshold);

            if (facilityId.HasValue && facilityId != Guid.Empty)
            {
                query = query.Where(m => m.FacilityId == facilityId.Value);
            }

            var pending = await query
                .OrderBy(m => m.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (!pending.Any()) return;

            if (!pending.Any()) return;

            _logger.LogInformation("Processing {Count} outbox messages sequentially...", pending.Count);

            // Phase 3: Sequential processing to ensure DbContext thread-safety (CRITICAL for SQLite)
            foreach (var messageEntity in pending)
            {
                if (ct.IsCancellationRequested) break;

                // Each message gets its own scope/context for maximum isolation and safety
                using var messageScope = _scopeFactory.CreateScope();
                var messageContext = messageScope.ServiceProvider.GetRequiredService<AppDbContext>();

                var message = await messageContext.OutboxMessages
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.Id == messageEntity.Id, ct);

                if (message == null || message.IsProcessed) continue;

                try
                {
                    bool success = await DispatchOutboxMessageAsync(message, ct);
                    
                    if (success)
                    {
                        message.IsProcessed = true;
                        message.ProcessedAt = DateTime.UtcNow;
                        _logger.LogInformation("Marked outbox message {MessageId} as processed", message.Id);
                    }
                    else
                    {
                        message.ErrorCount++;
                        if (message.ErrorCount >= 5)
                        {
                            message.IsDeadLetter = true;
                            _logger.LogWarning("Outbox message {MessageId} reached max retries and is now a DEAD LETTER", message.Id);
                        }
                    }

                    await messageContext.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to process outbox message {message.Id} for {message.EntityType}");
                }
            }
        }

        private async Task<bool> DispatchOutboxMessageAsync(OutboxMessage message, CancellationToken ct)
        {
            try
            {
                switch (message.EntityType)
                {
                    case "Sale":
                        return await SyncSnapshotAsync<SaleModel>(message, MapToSaleModel, ct);
                    case "SaleItem":
                        return await SyncSnapshotAsync<SupabaseSaleItem>(message, MapToSupabaseSaleItem, ct);
                    case "AccessEvent":
                        return await SyncSnapshotAsync<SupabaseAccessEvent>(message, MapToSupabaseAccessEvent, ct);
                    case "StaffMember":
                        var staffSuccess = await SyncSnapshotAsync<SupabaseStaffMember>(message, MapToSupabaseStaffMember, ct);
                        
                        // Check if staff has pending auth
                        var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.ContentJson);
                        var authStatus = GetVal<string>(snapshot, "AuthSyncStatus");
                        
                        if (authStatus == "pending")
                        {
                            await ProcessPendingStaffAuthAsync(message, snapshot, ct);
                        }
                        
                        return staffSuccess;
                    // LOCAL-ONLY: MembershipPlan and MembershipPlanFacility cases removed
                    case "Registration":
                        return await SyncSnapshotAsync<SupabaseRegistration>(message, MapToSupabaseRegistration, ct);
                    default:
                        // Delegate to facility-specific strategies
                        var currentFacilityType = _facilityContext.CurrentFacility;
                        var strategy = _strategies.FirstOrDefault(s => s.FacilityType == currentFacilityType);
                        if (strategy != null)
                        {
                            bool strategyHandled = await strategy.HandleOutboxMessageAsync(message, ct);
                            if (strategyHandled) return true;
                        }

                        _logger.LogWarning($"No sync handler implemented for entity type: {message.EntityType}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Critical error dispatching outbox message {message.Id}");
                return false;
            }
        }

        private async Task ProcessPendingStaffAuthAsync(OutboxMessage message, Dictionary<string, JsonElement> snapshot, CancellationToken ct)
        {
            var email = GetVal<string>(snapshot, "PendingAuthEmail");
            var staffId = GetVal<Guid>(snapshot, "Id");
            
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Staff {StaffId} has pending auth but no email", staffId);
                return;
            }
            
            try
            {
                _logger.LogInformation("Processing pending auth for staff {StaffId} ({Email})", staffId, email);
                
                // Generate temporary password
                var tempPassword = Guid.NewGuid().ToString();
                
                // Create Supabase Auth account
                var authResult = await _supabase.Auth.SignUp(email, tempPassword);
                
                if (authResult?.User != null)
                {
                    _logger.LogInformation("Auth created successfully for staff {StaffId}", staffId);
                    
                    // Update staff record with Supabase ID
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var staff = await context.StaffMembers.FindAsync(new object[] { staffId }, ct);
                    
                    if (staff != null)
                    {
                        staff.MarkAuthCompleted(authResult.User.Id);
                        await context.SaveChangesAsync(ct);
                        
                        // TODO: Send password reset email
                        _logger.LogInformation("Staff {StaffId} should receive password reset email at {Email}", staffId, email);
                    }
                }
                else
                {
                    _logger.LogWarning("Auth creation returned null for staff {StaffId}", staffId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create auth for staff {StaffId}", staffId);
                
                // Update staff to mark auth as failed
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var staff = await context.StaffMembers.FindAsync(new object[] { staffId }, ct);
                    if (staff != null)
                    {
                        staff.MarkAuthFailed(ex.Message);
                        await context.SaveChangesAsync(ct);
                    }
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to update auth status for staff {StaffId}", staffId);
                }
            }
        }

        // ... existing code ...

        // --- SNAPSHOT MAPPERS CONSOLIDATED ---

        private async Task<bool> SyncSnapshotAsync<TSupabaseModel>(
            OutboxMessage message, 
            Func<Dictionary<string, JsonElement>, TSupabaseModel> mapper, 
            CancellationToken ct) 
            where TSupabaseModel : BaseModel, new()
        {
            if (message.Action == "Deleted")
            {
                await _supabase.From<TSupabaseModel>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, message.EntityId)
                    .Delete(cancellationToken: ct);
                return true;
            }

            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.ContentJson);

            // [DIAGNOSTIC] Log snapshot keys for debugging
            _logger.LogDebug($"Outbox Snapshot Keys for {message.EntityType}: {string.Join(", ", snapshot.Keys)}");

            var supabaseModel = mapper(snapshot);

            // JIT REPAIR: Ensure FacilityId and TenantId are populated from the most reliable sources.
            var targetFacilityId = message.FacilityId != Guid.Empty ? message.FacilityId : (_currentFacilityId ?? Guid.Empty);
            var targetTenantId = _tenantService.GetTenantId() ?? message.TenantId;

            // Patch FacilityId
            if (targetFacilityId != Guid.Empty)
            {
                var facilityIdProp = typeof(TSupabaseModel).GetProperty("FacilityId");
                if (facilityIdProp != null && facilityIdProp.PropertyType == typeof(Guid))
                {
                    var currentVal = (Guid)facilityIdProp.GetValue(supabaseModel);
                    if (currentVal == Guid.Empty)
                    {
                         _logger.LogWarning("[Sync Repair] Patching empty FacilityId for {Type} {Id} with {NewId}", 
                             message.EntityType, message.EntityId, targetFacilityId);
                         facilityIdProp.SetValue(supabaseModel, targetFacilityId);
                    }
                }
            }

            // Patch TenantId
            if (targetTenantId != Guid.Empty)
            {
                var tenantIdProp = typeof(TSupabaseModel).GetProperty("TenantId");
                if (tenantIdProp != null && tenantIdProp.PropertyType == typeof(Guid))
                {
                    var currentVal = (Guid)tenantIdProp.GetValue(supabaseModel);
                    if (currentVal == Guid.Empty)
                    {
                         _logger.LogWarning("[Sync Repair] Patching empty TenantId for {Type} {Id} with {NewId}", 
                             message.EntityType, message.EntityId, targetTenantId);
                         tenantIdProp.SetValue(supabaseModel, targetTenantId);
                    }
                }
            }

            // CRITICAL GUARD: Never sync to Supabase without a TenantId. 
            // This prevents "orphaned" data that could be visible to other tenants or cause RLS failures.
            var finalTenantId = (Guid?)typeof(TSupabaseModel).GetProperty("TenantId")?.GetValue(supabaseModel);
            if (finalTenantId == null || finalTenantId == Guid.Empty)
            {
                _logger.LogError("[Sync Critical] Aborting sync for {Type} {Id}: No TenantId found after JIT repair.", 
                    message.EntityType, message.EntityId);
                return false;
            }

            _logger.LogInformation($"[Sync] Attempting Upsert for {message.EntityType} {message.EntityId} to Supabase...");
            
            var result = await _supabase.From<TSupabaseModel>().Upsert(supabaseModel, cancellationToken: ct);
            
            if (!result.ResponseMessage.IsSuccessStatusCode)
            {
                var errorBody = await result.ResponseMessage.Content.ReadAsStringAsync(ct);
                _logger.LogWarning($"Sync failed for {message.EntityType} {message.EntityId}: {result.ResponseMessage.StatusCode} - {errorBody}");
                
                // Only show toast for critical data errors (403/Forbidden is often RLS)
                if (result.ResponseMessage.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _toastService.ShowError($"Permission Denied for {message.EntityType}: Check if your account matches the facility. {errorBody}", "RLS Error");
                }
            }
            else
            {
                _logger.LogInformation($"[Sync] Successfully synced {message.EntityType} {message.EntityId}");
            }
            
            return result.ResponseMessage.IsSuccessStatusCode;
        }

        public async Task CleanupOutboxAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Delete processed or dead letter messages older than 7 days
                var threshold = DateTime.UtcNow.AddDays(-7);
                
                var oldMessages = await context.OutboxMessages
                    .IgnoreQueryFilters()
                    .Where(m => (m.IsProcessed || m.IsDeadLetter) && m.CreatedAt < threshold)
                    .ToListAsync(ct);

                if (oldMessages.Any())
                {
                    _logger.LogInformation("Cleaning up {Count} old outbox messages...", oldMessages.Count);
                    context.OutboxMessages.RemoveRange(oldMessages);
                    await context.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup outbox messages");
            }
        }

        private T? GetVal<T>(Dictionary<string, JsonElement> dict, string key)
        {
            // Case-insensitive lookup to handle potential property name mismatches
            var actualKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? key;

            if (!dict.TryGetValue(actualKey, out var element) || element.ValueKind == JsonValueKind.Null)
                return default;

            try
            {
                // Direct handle for common types to avoid overhead
                if (typeof(T) == typeof(Guid)) return (T)(object)element.GetGuid();
                if (typeof(T) == typeof(Guid?)) return (T)(object)element.GetGuid();
                if (typeof(T) == typeof(string)) return (T)(object)element.GetString()!;
                if (typeof(T) == typeof(decimal)) return (T)(object)element.GetDecimal();
                if (typeof(T) == typeof(int)) return (T)(object)element.GetInt32();
                if (typeof(T) == typeof(bool)) return (T)(object)element.GetBoolean();
                if (typeof(T) == typeof(DateTime)) return (T)(object)element.GetDateTime();
                if (typeof(T) == typeof(DateTimeOffset)) return (T)(object)element.GetDateTimeOffset();
                
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch (Exception ex)
            {
                _logger.LogTrace("GetVal failed for key {Key} and type {Type}: {Message}", key, typeof(T).Name, ex.Message);
                return default;
            }
        }

        // --- PRE-CONSOLIDATION MAPPING SECTION REMOVED (DUPLICATE) ---
        // MapToSupabaseMember is defined later in the consolidated snapshot mappers section


        public async Task<bool> PullChangesAsync(CancellationToken ct, Guid? facilityId = null)
        {
            // Phase 1: Try to restore session from storage if not already active
            if (_supabase.Auth.CurrentSession == null)
            {
                var restored = await TryRestoreSupabaseSessionAsync();
                if (!restored)
                {
                    _logger.LogWarning("Sync: No active Supabase session and unable to restore from storage. Skipping Pull. (User may be in offline mode)");
                    _toastService.ShowInfo("Online sync available after logging in.", "Cloud Offline");
                    UpdateStatus(SyncStatus.Offline);
                    return false;
                }
            }

            if (!await _syncSemaphore.WaitAsync(0, ct)) return false;

            try
            {
                UpdateStatus(SyncStatus.Syncing);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var targetFacilityId = facilityId ?? _facilityContext.CurrentFacilityId;
                
                if (targetFacilityId == Guid.Empty)
                {
                    _logger.LogWarning("PullChangesAsync: No facility context (passed or global). Skipping.");
                    return false;
                }

                var syncKey = $"{LAST_SYNC_KEY}_{targetFacilityId}";
                var lastSyncStr = await _secureStorage.GetAsync(syncKey);
                var lastSync = string.IsNullOrEmpty(lastSyncStr) 
                    ? DateTimeOffset.MinValue 
                    : DateTimeOffset.Parse(lastSyncStr);

                // Pull Parents before Children
                // LOCAL-FIRST: Members and Products are managed locally, but we pull updates for cross-device visibility.
                
                await PullStaffMembersAsync(context, lastSync, ct);
                await PullRegistrationsAsync(context, lastSync, ct);

                // Pull Facility-Specific Data via Strategies
                var currentFacilityType = _facilityContext.CurrentFacility;
                var strategy = _strategies.FirstOrDefault(s => s.FacilityType == currentFacilityType);
                if (strategy != null)
                {
                    await strategy.PullSpecificDataAsync(context, lastSync, ct);
                }

                await _secureStorage.SetAsync(syncKey, DateTimeOffset.UtcNow.ToString("O"));
                UpdateStatus(SyncStatus.Idle);
                _logger.LogInformation($"[Sync] Pull completed successfully. Next sync will be relative to {DateTimeOffset.UtcNow}");
                
                SyncCompleted?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus(SyncStatus.Error);
                _logger.LogError(ex, "Sync Pull Error - Timestamp NOT updated. Will retry previous interval.");
                _toastService.ShowError($"Failed to fetch updates: {ex.Message}", "Sync Pull Error");
                return false;
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }


        private async Task PullStaffMembersAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseStaffMember>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.UpdatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                var remoteModels = remoteData.Models;
                var remoteIds = remoteModels.Select(x => x.Id).ToList();

                var existingEntities = await context.StaffMembers
                    .IgnoreQueryFilters()
                    .Where(x => remoteIds.Contains(x.Id))
                    .ToListAsync(ct);

                var existingMap = existingEntities.ToDictionary(x => x.Id);
                var newEntities = new List<StaffMember>();

                foreach (var remote in remoteModels)
                {
                    if (existingMap.TryGetValue(remote.Id, out var existing))
                    {
                        // FIX: Only update if remote is actually newer than local
                        if (remote.UpdatedAt <= (existing.UpdatedAt ?? existing.CreatedAt))
                        {
                            _logger.LogDebug("[Sync] Skipping staff update for {Id}: Local is newer or same.", remote.Id);
                            continue;
                        }

                        var emailResult = Email.Create(remote.Email);
                        var email = emailResult.IsSuccess ? emailResult.Value : Email.Create("unknown@titan.com").Value;
                        var phoneResult = PhoneNumber.Create(remote.PhoneNumber);
                        var phone = phoneResult.IsSuccess ? phoneResult.Value : PhoneNumber.None;

                        // Update using the domain model's UpdateDetails method to respect private setters
                        existing.UpdateDetails(
                            remote.FullName ?? string.Empty,
                            email,
                            phone,
                            (StaffRole)remote.Role,
                            remote.Salary,
                            remote.PaymentDay
                        );

                        if (remote.CardId != null) existing.SetCardId(remote.CardId);
                        if (remote.SupabaseUserId.HasValue) existing.SetSupabaseUserId(remote.SupabaseUserId.Value.ToString());
                        
                        existing.IsSynced = true;
                    }
                    else
                    {
                        var staff = MapToDomain(remote);
                        staff.IsSynced = true;
                        newEntities.Add(staff);
                    }
                }

                if (newEntities.Any()) await context.StaffMembers.AddRangeAsync(newEntities, ct);
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling staff members.");
                throw;
            }
        }



        // Outbox Snapshot Mappers
        // --- CONSOLIDATED SNAPSHOT MAPPERS ---

        /* 
        // LOCAL-ONLY: MapToSupabaseMembershipPlan removed
        private SupabaseMembershipPlan MapToSupabaseMembershipPlan(Dictionary<string, JsonElement> snapshot)
        {
            ...
        }
        */


        private SaleModel MapToSaleModel(Dictionary<string, JsonElement> snapshot)
        {
            return new SaleModel
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                MemberId = GetVal<Guid>(snapshot, "MemberId"),
                TotalAmount = GetVal<decimal>(snapshot, "TotalAmount_Amount"),
                Timestamp = GetVal<DateTime>(snapshot, "Timestamp"),
                PaymentMethod = GetVal<int>(snapshot, "PaymentMethod")
            };
        }

        private SupabaseRegistration MapToSupabaseRegistration(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseRegistration
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                FullName = GetVal<string>(snapshot, "FullName") ?? "",
                Email = GetVal<string>(snapshot, "Email"),
                PhoneNumber = GetVal<string>(snapshot, "PhoneNumber"),
                Source = GetVal<string>(snapshot, "Source") ?? "",
                Status = GetVal<int>(snapshot, "Status"),
                Notes = GetVal<string>(snapshot, "Notes"),
                PreferredPlanId = GetVal<Guid?>(snapshot, "PreferredPlanId"),
                PreferredStartDate = GetVal<DateTime?>(snapshot, "PreferredStartDate"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt"),
                UpdatedAt = GetVal<DateTime>(snapshot, "UpdatedAt")
            };
        }

        private SupabaseStaffMember MapToSupabaseStaffMember(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseStaffMember
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                FullName = GetVal<string>(snapshot, "FullName") ?? "",
                Email = GetVal<string>(snapshot, "Email"),
                PhoneNumber = GetVal<string>(snapshot, "PhoneNumber") ?? GetVal<string>(snapshot, "Phone"),
                Role = GetVal<int>(snapshot, "Role"),
                IsActive = GetVal<bool>(snapshot, "IsActive"),
                IsOwner = GetVal<bool>(snapshot, "IsOwner"),
                Salary = GetVal<decimal>(snapshot, "Salary"),
                PaymentDay = GetVal<int>(snapshot, "PaymentDay"),
                CardId = GetVal<string>(snapshot, "CardId"),
                PermissionsJson = snapshot.ContainsKey("Permissions") ? JToken.Parse(snapshot["Permissions"].GetRawText()) : null,
                AllowedModulesJson = snapshot.ContainsKey("AllowedModules") ? JToken.Parse(snapshot["AllowedModules"].GetRawText()) : null,
                SupabaseUserId = GetVal<Guid?>(snapshot, "SupabaseUserId"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt"),
                UpdatedAt = GetVal<DateTime>(snapshot, "UpdatedAt")
            };
        }


        private SupabaseSaleItem MapToSupabaseSaleItem(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseSaleItem
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                SaleId = GetVal<Guid>(snapshot, "SaleId"),
                ProductId = GetVal<Guid?>(snapshot, "ProductId"),
                NameSnapshot = GetVal<string>(snapshot, "ProductNameSnapshot") ?? "",
                Quantity = GetVal<int>(snapshot, "Quantity"),
                PriceSnapshot = GetVal<decimal>(snapshot, "UnitPriceSnapshot_Amount"),
                TaxAmount = 0
            };
        }

        private SupabaseAccessEvent MapToSupabaseAccessEvent(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseAccessEvent
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                MemberId = GetVal<string>(snapshot, "MemberId"),
                TurnstileId = GetVal<Guid?>(snapshot, "TurnstileId"),
                Status = GetVal<int?>(snapshot, "AccessStatus"),
                Reason = GetVal<string>(snapshot, "FailureReason"),
                ScannedAt = GetVal<DateTime>(snapshot, "Timestamp")
            };
        }

        /* 
        // LOCAL-ONLY: MapToSupabaseMembershipPlanFacility removed
        private SupabaseMembershipPlanFacility MapToSupabaseMembershipPlanFacility(Dictionary<string, JsonElement> snapshot)
        {
            ...
        }
        */


        private StaffMember MapToDomain(SupabaseStaffMember model)
        {
            var emailResult = Email.Create(model.Email);
            var email = emailResult.IsSuccess ? emailResult.Value : Email.Create("unknown@titan.com").Value;
            
            var staff = StaffMember.ForLocalSync(
                model.Id,
                model.TenantId,
                model.FacilityId,
                model.FullName ?? "",
                email,
                (StaffRole)model.Role,
                model.IsActive,
                model.Salary,
                model.PaymentDay
            );

            if (model.CardId != null) staff.SetCardId(model.CardId);
            if (model.SupabaseUserId.HasValue) staff.SetSupabaseUserId(model.SupabaseUserId.Value.ToString());

            return staff;
        }

        private async Task PullRegistrationsAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseRegistration>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.UpdatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                var remoteModels = remoteData.Models;
                var remoteIds = remoteModels.Select(x => x.Id).ToList();

                var existingEntities = await context.Registrations
                    .IgnoreQueryFilters()
                    .Where(x => remoteIds.Contains(x.Id))
                    .ToListAsync(ct);

                var existingMap = existingEntities.ToDictionary(x => x.Id);
                var newEntities = new List<Registration>();

                foreach (var remote in remoteModels)
                {
                    if (existingMap.TryGetValue(remote.Id, out var existing))
                    {
                        // FIX: Only update if remote is actually newer than local
                        if (remote.UpdatedAt <= (existing.UpdatedAt ?? existing.CreatedAt))
                        {
                            _logger.LogDebug("[Sync] Skipping registration update for {Id}: Local is newer or same.", remote.Id);
                            continue;
                        }

                        // Simplified update for lead capture data
                        var updated = MapToDomain(remote);
                        context.Entry(existing).CurrentValues.SetValues(updated);
                        existing.IsSynced = true;
                    }
                    else
                    {
                        var registration = MapToDomain(remote);
                        registration.IsSynced = true;
                        newEntities.Add(registration);
                    }
                }

                if (newEntities.Any()) await context.Registrations.AddRangeAsync(newEntities, ct);
                await context.SaveChangesAsync(ct);
                _logger.LogInformation($"[Sync] Pulled {newEntities.Count} new registrations from Supabase.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling registrations.");
                throw;
            }
        }

        private Management.Domain.Models.Registration MapToDomain(SupabaseRegistration model)
        {
            var emailObj = Email.Create(model.Email ?? "unknown@titan.com").Value;
            var phoneObj = PhoneNumber.Create(model.PhoneNumber ?? "").Value;

            var registration = Management.Domain.Models.Registration.Submit(
                model.FullName ?? "",
                emailObj,
                phoneObj,
                model.Source ?? "Website",
                model.PreferredPlanId,
                model.PreferredStartDate,
                model.Notes ?? ""
            ).Value;

            // Overwrite ID
            var idProp = typeof(Management.Domain.Primitives.Entity).GetProperty("Id");
            idProp?.SetValue(registration, model.Id);

            registration.TenantId = model.TenantId;
            registration.FacilityId = model.FacilityId;
            
            // RegistrationStatus is internal in Domain, but we can set it via property if accessible or reflection
            var statusProp = typeof(Management.Domain.Models.Registration).GetProperty("Status");
            statusProp?.SetValue(registration, (RegistrationStatus)model.Status);

            return registration;
        }

        public async Task ResetSyncContextAsync()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var syncKey = $"{LAST_SYNC_KEY}_{facilityId}";
            await _secureStorage.RemoveAsync(syncKey);
            _logger.LogWarning("[Sync] Local sync timestamp for facility {FacilityId} has been RESET. Next pull will be a FULL sync.", facilityId);
        }

        /// <summary>
        /// Attempts to restore Supabase session from stored session data.
        /// Returns true if session was restored or already exists, false otherwise.
        /// </summary>
        private async Task<bool> TryRestoreSupabaseSessionAsync()
        {
            try
            {
                // If session already exists, we're good
                if (_supabase.Auth.CurrentSession != null)
                {
                    return true;
                }

                // Try to load stored session
                var storedSession = await _sessionStorage.LoadSessionAsync();
                if (storedSession == null || storedSession.IsExpired)
                {
                    _logger.LogDebug("Sync: No valid stored session found.");
                    return false;
                }

                // Phase 3: Circuit Breaker Check
                if (_sessionFailureCount >= MaxSessionFailures && (DateTime.UtcNow - _lastSessionFailure) < SessionCooldown)
                {
                    _logger.LogWarning("Sync: Session restoration is in circuit-breaker cooldown. Skipping cloud operations for 5 minutes.");
                    return false;
                }

                // Check if this is an offline session (fake tokens)
                if (storedSession.AccessToken == "OFFLINE_ACCESS_TOKEN" || 
                    storedSession.RefreshToken == "OFFLINE_REFRESH_TOKEN")
                {
                    _logger.LogWarning("Sync: Stored session is offline-only. Cannot sync without real Supabase credentials. User must login with email/password to enable cloud sync.");
                    return false;
                }

                // The Supabase client may have its own session persistence mechanism.
                // Try to trigger a session refresh if we have a valid refresh token.
                // Note: RefreshSession() uses the current session's refresh token, so we need
                // to manually set the session first if possible, or rely on Supabase's auto-restore.
                
                // Attempt to restore Supabase session
                try
                {
                    _logger.LogInformation("Sync: Restoring Supabase session for {Email}...", storedSession.Email);
                    var session = await _supabase.Auth.SetSession(storedSession.AccessToken, storedSession.RefreshToken);
                    
                    if (session?.User != null)
                    {
                        _logger.LogInformation("Sync: Supabase session restored successfully for {Email}", storedSession.Email);
                        
                        // Populate context services for this background worker thread/instance
                        _tenantService.SetTenantId(storedSession.TenantId);
                        _tenantService.SetUserId(storedSession.StaffId);
                        _tenantService.SetRole(storedSession.Role);
                        
                        // Cache for JIT Repair
                        _currentFacilityId = storedSession.FacilityId;
                        
                        return true;
                    }

                    _logger.LogWarning("Sync: SetSession returned null user for {Email}", storedSession.Email);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sync: Error calling SetSession for {Email}", storedSession.Email);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _sessionFailureCount++;
                _lastSessionFailure = DateTime.UtcNow;
                _logger.LogError(ex, "Sync: Error attempting to restore Supabase session.");
                return false;
            }
        }
    }
}
