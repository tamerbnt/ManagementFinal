using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Management.Infrastructure.Data.Models;
using Management.Infrastructure.Integrations.Supabase.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest.Models;
using Management.Domain.Models;

namespace Management.Infrastructure.Services.Sync
{
    public class GymSyncStrategy : IFacilitySyncStrategy
    {
        private readonly Supabase.Client _supabase;
        private readonly ITenantService _tenantService;
        private readonly ILogger<GymSyncStrategy> _logger;

        public FacilityType FacilityType => FacilityType.Gym;

        public GymSyncStrategy(
            Supabase.Client supabase,
            ITenantService tenantService,
            ILogger<GymSyncStrategy> logger)
        {
            _supabase = supabase;
            _tenantService = tenantService;
            _logger = logger;
        }

        public async Task PullSpecificDataAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            _logger.LogInformation("GymSyncStrategy: Pulling Gym-specific data...");
            await PullGymSettingsAsync(context, lastSync, ct);
            await PullFacilitySchedulesAsync(context, lastSync, ct);
            await PullTurnstilesAsync(context, lastSync, ct);
        }

        public async Task<bool> HandleOutboxMessageAsync(Management.Domain.Models.OutboxMessage message, CancellationToken ct)
        {
            switch (message.EntityType)
            {
                case "GymSettings":
                    return await SyncSnapshotAsync<SupabaseGymSettings>(message, MapToSupabaseGymSettings, ct);
                case "FacilitySchedule":
                    return await SyncSnapshotAsync<SupabaseFacilitySchedule>(message, MapToSupabaseFacilitySchedule, ct);
                case "Turnstile":
                    return await SyncSnapshotAsync<SupabaseTurnstile>(message, MapToSupabaseTurnstile, ct);
                default:
                    return false; // Not handled by this strategy
            }
        }

        private async Task PullGymSettingsAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseGymSettings>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.UpdatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                foreach (var remote in remoteData.Models)
                {
                    var existing = await context.GymSettings.FindAsync(new object[] { remote.Id }, ct);
                    if (existing != null)
                    {
                        // FIX: Only update if remote is actually newer than local
                        if (remote.UpdatedAt <= existing.UpdatedAt)
                        {
                            _logger.LogDebug("[Sync] Skipping gym settings update for {Id}: Local is newer or same.", remote.Id);
                            continue;
                        }

                        existing.GymName = remote.GymName;
                        existing.Address = remote.Address;
                        existing.PhoneNumber = remote.Phone ?? "";
                        existing.Email = remote.Email;
                        existing.OperatingHoursJson = remote.OperatingHoursJson;
                        existing.IsSynced = true;
                    }
                }
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling gym settings.");
            }
        }

        private async Task PullFacilitySchedulesAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseFacilitySchedule>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.UpdatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                foreach (var remote in remoteData.Models)
                {
                    var existing = await context.FacilitySchedules.FindAsync(new object[] { remote.Id }, ct);
                    if (existing != null)
                    {
                        // FIX: Only update if remote is actually newer than local
                        if (remote.UpdatedAt <= existing.UpdatedAt)
                        {
                            _logger.LogDebug("[Sync] Skipping schedule update for {Id}: Local is newer or same.", remote.Id);
                            continue;
                        }

                        existing.DayOfWeek = remote.DayOfWeek;
                        existing.StartTime = TimeSpan.Parse(remote.StartTime);
                        existing.EndTime = TimeSpan.Parse(remote.EndTime);
                        existing.RuleType = remote.RuleType;
                        existing.IsSynced = true;
                    }
                }
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling facility schedules.");
            }
        }

        private async Task PullTurnstilesAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseTurnstile>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.CreatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                foreach (var remote in remoteData.Models)
                {
                    var existing = await context.Turnstiles.FindAsync(new object[] { remote.Id }, ct);
                    if (existing != null)
                    {
                        existing.UpdateStatus(remote.IsActive ? TurnstileStatus.Operational : TurnstileStatus.Offline);
                        // existing.IsSynced = true; // Handled by SaveChanges if implemented
                    }
                }
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling turnstiles.");
            }
        }

        private async Task<bool> SyncSnapshotAsync<TSupabaseModel>(
            Management.Domain.Models.OutboxMessage message,
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
            var supabaseModel = mapper(snapshot);

            var result = await _supabase.From<TSupabaseModel>().Upsert(supabaseModel, cancellationToken: ct);
            return result.ResponseMessage.IsSuccessStatusCode;
        }

        private SupabaseGymSettings MapToSupabaseGymSettings(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseGymSettings
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                GymName = GetVal<string>(snapshot, "GymName") ?? "",
                Address = GetVal<string>(snapshot, "Address"),
                // Fixed: The snapshot contains 'PhoneNumber' from the domain model, but Supabase model property is 'Phone'
                Phone = GetVal<string>(snapshot, "PhoneNumber") ?? GetVal<string>(snapshot, "Phone"),
                Email = GetVal<string>(snapshot, "Email"),
                OperatingHoursJson = GetVal<string>(snapshot, "OperatingHoursJson"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt"),
                UpdatedAt = GetVal<DateTime>(snapshot, "UpdatedAt")
            };
        }

        private SupabaseFacilitySchedule MapToSupabaseFacilitySchedule(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseFacilitySchedule
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                DayOfWeek = GetVal<int>(snapshot, "DayOfWeek"),
                StartTime = GetVal<string>(snapshot, "StartTime") ?? "00:00:00",
                EndTime = GetVal<string>(snapshot, "EndTime") ?? "23:59:59",
                RuleType = GetVal<int>(snapshot, "RuleType"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt"),
                UpdatedAt = GetVal<DateTime>(snapshot, "UpdatedAt")
            };
        }

        private SupabaseAccessEvent MapToSupabaseAccessEvent(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseAccessEvent
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                MemberId = GetVal<string>(snapshot, "CardId"), // Local "CardId" maps to Remote "MemberId"
                TurnstileId = GetVal<Guid>(snapshot, "TurnstileId"),
                Status = (int)GetVal<int>(snapshot, "AccessStatus"),
                Reason = GetVal<string>(snapshot, "FailureReason"),
                ScannedAt = GetVal<DateTime>(snapshot, "Timestamp"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt")
            };
        }

        private SupabaseTurnstile MapToSupabaseTurnstile(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseTurnstile
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                Name = GetVal<string>(snapshot, "Name") ?? "",
                IpAddress = GetVal<string>(snapshot, "IpAddress"),
                Port = GetVal<int>(snapshot, "Port"),
                IsActive = GetVal<bool>(snapshot, "IsActive"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt")
            };
        }

        private T? GetVal<T>(Dictionary<string, JsonElement> dict, string key)
        {
            // Case-insensitive lookup to handle potential property name mismatches between domain/dto
            var actualKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? key;

            if (!dict.TryGetValue(actualKey, out var element) || element.ValueKind == JsonValueKind.Null)
                return default;

            try
            {
                if (typeof(T) == typeof(Guid)) return (T)(object)element.GetGuid();
                if (typeof(T) == typeof(string)) return (T)(object)element.GetString()!;
                if (typeof(T) == typeof(decimal)) return (T)(object)element.GetDecimal();
                if (typeof(T) == typeof(int)) return (T)(object)element.GetInt32();
                if (typeof(T) == typeof(bool)) return (T)(object)element.GetBoolean();
                if (typeof(T) == typeof(DateTime)) return (T)(object)element.GetDateTime();
                
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return default;
            }
        }
    }
}
