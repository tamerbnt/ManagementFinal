using Management.Domain.Models;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Infrastructure.Configuration;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.Stores;

namespace Management.Infrastructure.Services
{
    public class SyncWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SyncStore _syncStore;
        private readonly IConnectionService _connectionService;

        public SyncWorker(IServiceProvider serviceProvider, IConnectionService connectionService, SyncStore syncStore)
        {
            _serviceProvider = serviceProvider;
            _connectionService = connectionService;
            _syncStore = syncStore;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await _connectionService.IsInternetAvailableAsync())
                {
                    _syncStore.IsSyncing = true;
                    await ProcessOutboxAsync(stoppingToken);
                    _syncStore.IsSyncing = false;
                    _syncStore.LastSyncTime = DateTime.UtcNow;
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task ProcessOutboxAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
            var supabase = scope.ServiceProvider.GetRequiredService<Supabase.Client>();

            var pendingMessages = await context.OutboxMessages
                .Where(m => !m.IsProcessed && m.ErrorCount < 5)
                .OrderBy(m => m.CreatedAt)
                .Take(20)
                .ToListAsync(stoppingToken);

            foreach (var message in pendingMessages)
            {
                try
                {
                    await ResiliencePolicyRegistry.CloudRetryPolicy.ExecuteAsync(async () =>
                    {
                        // Logic to sync to Supabase based on EntityType and Action
                        // This is a simplified placeholder for actual Supabase CRUD
                        // e.g. await supabase.From(message.EntityType).Upsert(message.ContentJson);
                        
                        // Simulate cloud work
                        await Task.Delay(100); 
                    });

                    message.IsProcessed = true;
                    message.ProcessedAt = DateTime.UtcNow;
                }
                catch (DbUpdateConcurrencyException)
                {
                    message.IsConflict = true;
                    message.LastError = "Concurrency conflict: Remote data changed while offline.";
                    _syncStore.AddConflict(message);
                }
                catch (Exception ex)
                {
                    message.ErrorCount++;
                    message.LastError = ex.Message;
                }
            }

            await context.SaveChangesAsync(stoppingToken);
        }
    }
}
