using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces.App;
using Management.Application.DTOs;
using Management.Application.Notifications;
using Management.Infrastructure.Hardware;
using Management.Infrastructure.Configuration;
using MediatR;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Management.Application.Interfaces;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Domain.Events;
using Management.Domain.Primitives;

namespace Management.Infrastructure.Services
{
    /// <summary>
    /// Background worker that bridges physical hardware events (RFID scans) 
    /// with application business logic and UI notifications.
    /// </summary>
    public class AccessMonitoringWorker : BackgroundService
    {
        private readonly IHardwareTurnstileService _turnstileService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AccessMonitoringWorker> _logger;
        private readonly TurnstileConfig _config;
        
        // Duplicate prevention: Store last scan time per card
        private readonly ConcurrentDictionary<string, DateTime> _lastScans = new();
        private readonly TimeSpan _minScanInterval = TimeSpan.FromSeconds(5);

        // Scan processing queue
        private readonly Channel<TurnstileScanEventArgs> _scanQueue = Channel.CreateBounded<TurnstileScanEventArgs>(100);

        public AccessMonitoringWorker(
            IHardwareTurnstileService turnstileService,
            IServiceScopeFactory scopeFactory,
            ILogger<AccessMonitoringWorker> logger,
            TurnstileConfig config)
        {
            _turnstileService = turnstileService;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Turnstile monitoring worker starting...");

            _turnstileService.CardScanned += OnCardScanned;
            
            // 1. Health & Connection Loop
            _ = Task.Run(async () => 
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!_turnstileService.IsConnected && _turnstileService.IsSdkAvailable)
                        {
                            var connected = await _turnstileService.ConnectAsync(_config.IpAddress, _config.Port);
                            if (connected) await _turnstileService.SyncOfflineLogsAsync();
                        }
                        else if (_turnstileService.IsConnected)
                        {
                            await _turnstileService.PingAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in turnstile health check loop");
                    }
                    
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }, stoppingToken);

            // 2. Scan Processing Loop
            try
            {
                await foreach (var scan in _scanQueue.Reader.ReadAllAsync(stoppingToken))
                {
                    await ProcessScanAsync(scan, stoppingToken);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void OnCardScanned(object? sender, TurnstileScanEventArgs e)
        {
            // Deduplication
            if (_lastScans.TryGetValue(e.CardId, out var lastTime) && (DateTime.UtcNow - lastTime < _minScanInterval))
            {
                return;
            }

            _lastScans[e.CardId] = DateTime.UtcNow;

            if (!_scanQueue.Writer.TryWrite(e))
            {
                _logger.LogWarning("Scan queue full! Dropping scan for {CardId}", e.CardId);
            }
        }

        private async Task ProcessScanAsync(TurnstileScanEventArgs e, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing hardware scan: {CardId}", e.CardId);

                // Resolve Scoped Services
                using var scope = _scopeFactory.CreateScope();
                var memberAccessService = scope.ServiceProvider.GetRequiredService<IMemberAccessService>();

                // Delegate to the unified service to ensure 1:1 parity with manual UI buttons
                await memberAccessService.ProcessAccessFlowAsync(
                    e.CardId, 
                    _config.FacilityId, 
                    e.Direction, 
                    e.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process turnstile scan via unified service for {CardId}", e.CardId);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _turnstileService.CardScanned -= OnCardScanned;
            _scanQueue.Writer.TryComplete();
            await base.StopAsync(cancellationToken);
        }
    }
}
