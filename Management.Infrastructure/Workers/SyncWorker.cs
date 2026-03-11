using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Infrastructure.Data;
using Management.Domain.Services;
using Management.Application.Interfaces.App;

namespace Management.Infrastructure.Workers
{
    public class SyncWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Management.Domain.Services.IConnectionService _connectionService;
        private readonly IFacilityContextService _facilityContextService;
        private readonly ILogger<SyncWorker> _logger;
        private readonly TimeSpan _baseSyncInterval;
        private DateTime _lastSecondarySync = DateTime.MinValue;
        private readonly TimeSpan _secondarySyncInterval = TimeSpan.FromHours(1);

        public SyncWorker(
            IServiceScopeFactory scopeFactory,
            Management.Domain.Services.IConnectionService connectionService,
            IFacilityContextService facilityContextService,
            ILogger<SyncWorker> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _connectionService = connectionService;
            _facilityContextService = facilityContextService;
            _logger = logger;
            
            // Phase 1: Configurable sync interval with validation
            var intervalMinutes = configuration.GetValue<int?>("SyncIntervalMinutes");
            
            if (!intervalMinutes.HasValue || intervalMinutes.Value < 1)
            {
                _logger.LogWarning("Invalid or missing SyncIntervalMinutes in config, using default: 5 minutes");
                intervalMinutes = 5;
            }
            
            if (intervalMinutes.Value > 60)
            {
                _logger.LogWarning("SyncIntervalMinutes too high ({Value}), capping at 60 minutes", intervalMinutes.Value);
                intervalMinutes = 60;
            }
            
            _baseSyncInterval = TimeSpan.FromMinutes(intervalMinutes.Value);
            _logger.LogInformation("Sync interval configured: {Interval}", _baseSyncInterval);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Sync Worker started with base interval: {Interval}", _baseSyncInterval);

            int consecutiveEmptyRuns = 0;
            int consecutiveFailures = 0;
            const int MaxFailuresBeforeBackoff = 3;

            try
            {
                // Initial sync attempt
                await PerformSyncAsync(stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Phase 1: Dynamic interval with backoff
                    var backoffMultiplier = Math.Min(consecutiveEmptyRuns, 6); // Max 6x = 30 min
                    var interval = _baseSyncInterval + TimeSpan.FromMinutes(_baseSyncInterval.TotalMinutes * backoffMultiplier);
                    
                    // Phase 1: Circuit breaker - wait longer after repeated failures
                    if (consecutiveFailures >= MaxFailuresBeforeBackoff)
                    {
                        interval = TimeSpan.FromMinutes(15);
                        _logger.LogWarning("Sync circuit breaker active. Waiting {Interval} before retry.", interval);
                    }
                    
                    _logger.LogDebug("Next sync in {Interval} (empty runs: {EmptyRuns}, failures: {Failures})", 
                        interval, consecutiveEmptyRuns, consecutiveFailures);
                    
                    await Task.Delay(interval, stoppingToken);
                    
                    var hadWork = await PerformSyncAsync(stoppingToken);
                    
                    if (hadWork)
                    {
                        consecutiveEmptyRuns = 0;
                        consecutiveFailures = 0;
                    }
                    else
                    {
                        consecutiveEmptyRuns++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sync Worker stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Sync Worker died unexpectedly.");
            }
        }

        private async Task<bool> PerformSyncAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Check Connectivity
                if (!await _connectionService.IsInternetAvailableAsync())
                {
                    _logger.LogWarning("Sync skipped: No internet connectivity.");
                    return false;
                }

                // Scope Creation
                using (var scope = _scopeFactory.CreateScope())
                {
                    var sync = scope.ServiceProvider.GetRequiredService<ISyncService>();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // 1. Determine which facilities to sync
                    var currentFacilityId = _facilityContextService.CurrentFacilityId;
                    var allFacilityIds = await context.Facilities
                        .IgnoreQueryFilters()
                        .Where(f => !f.IsDeleted)
                        .Select(f => f.Id)
                        .Distinct()
                        .ToListAsync(stoppingToken);

                    bool shouldSyncSecondary = (DateTime.UtcNow - _lastSecondarySync) > _secondarySyncInterval;
                    
                    var facilityIdsToSync = new List<Guid>();
                    if (currentFacilityId != Guid.Empty) 
                    {
                        facilityIdsToSync.Add(currentFacilityId);
                    }
                    
                    if (shouldSyncSecondary)
                    {
                        _logger.LogInformation("[Sync] Periodic secondary facility sync triggered.");
                        var secondaryIds = allFacilityIds.Where(id => id != currentFacilityId);
                        facilityIdsToSync.AddRange(secondaryIds);
                        _lastSecondarySync = DateTime.UtcNow;
                    }

                    if (!facilityIdsToSync.Any())
                    {
                        _logger.LogDebug("Sync: No local facilities found to sync.");
                        return false;
                    }

                    bool anyWorkDone = false;

                    // Phase 3: Parallel Facility Syncing
                    var syncTasks = facilityIdsToSync.Distinct().Select(async facilityId =>
                    {
                        if (stoppingToken.IsCancellationRequested) return;

                        bool isPrimary = (facilityId == currentFacilityId);
                        _logger.LogDebug("Starting Sync Cycle for Facility: {FacilityId} (Primary: {IsPrimary})", facilityId, isPrimary);

                        // Use a dedicated scope per facility to avoid DbContext thread-safety issues
                        using var facilityScope = _scopeFactory.CreateScope();
                        var facilitySync = facilityScope.ServiceProvider.GetRequiredService<ISyncService>();
                        var facilityContext = facilityScope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Phase 1: Push Changes (Scoped by Facility)
                        var pendingCount = await facilityContext.OutboxMessages
                            .IgnoreQueryFilters()
                            .Where(m => !m.IsProcessed && m.ErrorCount < 5 && m.FacilityId == facilityId)
                            .CountAsync(stoppingToken);

                        if (pendingCount > 0)
                        {
                            _logger.LogInformation("Pushing {Count} messages for Facility {FacilityId}", pendingCount, facilityId);
                            if (await facilitySync.PushChangesAsync(stoppingToken, facilityId)) anyWorkDone = true;
                        }

                        // Phase 2: Pull Changes (Scoped by Facility)
                        if (await facilitySync.PullChangesAsync(stoppingToken, facilityId)) anyWorkDone = true;
                    });

                    await Task.WhenAll(syncTasks);

                    return anyWorkDone;
                }
            }
            catch (Exception ex)
            {
                // Catch global exceptions to prevent Worker death
                _logger.LogError(ex, "Error during sync cycle.");
                return false;
            }
        }
    }
}
