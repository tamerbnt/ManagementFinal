using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Infrastructure.Integrations.Supabase.Models;
using Management.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Management.Application.Messages;
using Microsoft.EntityFrameworkCore;
using Management.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace Management.Infrastructure.Workers
{
    public class SnapshotSyncWorker : BackgroundService, IRecipient<SyncRequestedMessage>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SnapshotSyncWorker> _logger;
        private readonly IMessenger _messenger;
        private CancellationTokenSource? _syncTriggerCts;

        public SnapshotSyncWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<SnapshotSyncWorker> logger,
            IMessenger messenger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _messenger = messenger;
            
            // Register for priority sync requests
            _messenger.Register(this);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[SnapshotSync] Remote View background sync worker started.");

            // Initial delay to let the app finish booting and SQLite to stabilize
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformSyncAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break; // Graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SnapshotSync] Unexpected error during background sync. Retrying next cycle.");
                }

                _syncTriggerCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _syncTriggerCts.Token);
                
                try 
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), linkedCts.Token);
                }
                catch (TaskCanceledException)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                }
            }
        }

        public void Receive(SyncRequestedMessage message)
        {
            _logger.LogInformation("[SnapshotSync] Received priority sync request. Waking up worker...");
            _syncTriggerCts?.Cancel();
        }

        private async Task PerformSyncAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var supabaseClient = scope.ServiceProvider.GetRequiredService<Supabase.Client>();
                var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
                var historyProviders = scope.ServiceProvider.GetServices<IHistoryProvider>();
                var sessionStorage = scope.ServiceProvider.GetRequiredService<Management.Domain.Services.ISessionStorageService>();
                var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                var facilityContext = scope.ServiceProvider.GetRequiredService<IFacilityContextService>();

                var stored = await sessionStorage.LoadSessionAsync();
                if (stored == null) return;

                try
                {
                    await supabaseClient.Auth.SetSession(stored.AccessToken, stored.RefreshToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[SnapshotSync] Session restoration failed: {Error}", ex.Message);
                    return; 
                }

                var facilityMappings = await dbContext.Facilities
                    .Where(f => !f.IsDeleted)
                    .ToDictionaryAsync(f => f.Type, f => f.FacilityId, stoppingToken);

                foreach (var facility in facilityMappings)
                {
                    var facilityId = facility.Value;
                    var facilityType = facility.Key;
                    
                    var facilityEntity = await dbContext.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.FacilityId == facilityId, stoppingToken);
                    var tenantId = facilityEntity?.TenantId ?? Guid.Empty;

                    if (tenantId == Guid.Empty) continue;

                    // 3. SAFE IMPERSONATION (Ambient Context)
                    // This creates a thread-local override that doesn't touch the global UI state.
                    using (tenantService.Impersonate(tenantId))
                    using (facilityContext.Impersonate(facilityType, facilityId))
                    {
                        _logger.LogInformation("[SnapshotSync] Syncing facility {Id} ({Type}) with Tenant {Tenant}...", facilityId, facilityType, tenantId);

                        // 4. Calculate Dashboard Snapshot
                        var summary = await dashboardService.GetSummaryAsync(facilityId);
                        
                        if (summary != null)
                        {
                            var snapshotModel = new SupabaseDashboardSnapshot
                            {
                                FacilityId = facilityId,
                                TenantId = tenantId,
                                SnapshotData = JToken.FromObject(summary),
                                LastUpdatedAt = DateTime.UtcNow
                            };

                            await supabaseClient.From<SupabaseDashboardSnapshot>().Upsert(snapshotModel);
                            _logger.LogInformation("[SnapshotSync] Pushed dashboard snapshot for {Id}.", facilityId);
                        }

                        // 5. Calculate Daily History Summary for TODAY
                        var todayLocal = DateTime.Today;
                        var provider = historyProviders.FirstOrDefault(p => p.SegmentName == facilityType.ToString());

                        if (provider != null)
                        {
                            var history = await provider.GetHistoryAsync(facilityId, todayLocal, todayLocal.AddDays(1).AddSeconds(-1));
                            var historyList = history?.ToList() ?? new List<UnifiedHistoryEventDto>();

                            _logger.LogInformation("[SnapshotSync] Found {Count} events for {Facility} today.", historyList.Count, facilityType);

                            var historySummary = new SupabaseDailyHistorySummary
                            {
                                FacilityId = facilityId,
                                SummaryDate = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc),
                                Id = CreateDeterministicGuid(facilityId, todayLocal.ToString("yyyyMMdd")),
                                TenantId = tenantId,
                                TotalRevenue = historyList.Where(e => e.Type == HistoryEventType.Payment || e.Type == HistoryEventType.Sale).Sum(e => e.Amount ?? 0m),
                                CheckInCount = historyList.Count(e => e.Type == HistoryEventType.Access && e.IsSuccessful),
                                SalesCount = historyList.Count(e => e.Type == HistoryEventType.Sale),
                                EventsJson = JsonSerializer.Serialize(historyList.Take(50)),
                                LastUpdatedAt = DateTime.UtcNow
                            };

                            await supabaseClient.From<SupabaseDailyHistorySummary>().Upsert(historySummary);
                            _logger.LogInformation("[SnapshotSync] Pushed history summary for {Date}.", todayLocal.ToShortDateString());
                        }
                    }
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
            {
                _logger.LogWarning("[SnapshotSync] Database busy. Skipping cycle.");
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "[SnapshotSync] Sync failed.");
            }
        }

        private Guid CreateDeterministicGuid(Guid facilityId, string date)
        {
            var input = facilityId.ToString() + date;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return new Guid(hash);
            }
        }
    }
}
