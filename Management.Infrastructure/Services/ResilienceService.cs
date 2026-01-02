using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Management.Infrastructure.Data;
using Management.Domain.Models.Resilience;
using Serilog;

namespace Management.Infrastructure.Services
{
    public interface IResilienceService
    {
        bool IsOnline { get; }
        event EventHandler<bool>? ConnectivityChanged;
        Task QueueActionAsync(string entityType, OfflineActionType actionType, string payload);
        Task ProcessQueueAsync();
        ObservableCollection<OfflineAction> PendingActions { get; }
    }

    public class ResilienceService : IResilienceService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private bool _isOnline;
        public bool IsOnline
        {
            get => _isOnline;
            private set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    ConnectivityChanged?.Invoke(this, _isOnline);
                }
            }
        }

        public event EventHandler<bool>? ConnectivityChanged;
        public ObservableCollection<OfflineAction> PendingActions { get; } = new();

        public ResilienceService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;

            NetworkChange.NetworkAvailabilityChanged += (s, e) => 
            {
                IsOnline = e.IsAvailable;
            };
            
            // Initial check
            _isOnline = NetworkInterface.GetIsNetworkAvailable();
        }

        public async Task InitializeAsync()
        {
             // Clear existing to avoid duplication on re-init
             PendingActions.Clear();
             // Load existing pending actions from DB
             await LoadPendingActionsAsync();
        }

        private async Task LoadPendingActionsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
                
                // Fast-fail if connection string is missing or placeholder
                var connection = context.Database.GetDbConnection();
                if (string.IsNullOrEmpty(connection.ConnectionString) || connection.ConnectionString.Contains("REPLACE_WITH"))
                {
                    Serilog.Log.Warning("ResilienceService: Diagnostic - Connection string is invalid or missing. Skipping load.");
                    return;
                }

                var actions = await context.Set<OfflineAction>()
                    .Where(a => !a.IsDeleted)
                    .ToListAsync();
    
                foreach (var action in actions)
                {
                    PendingActions.Add(action);
                }
                
                Serilog.Log.Information($"ResilienceService: Loaded {PendingActions.Count} pending actions from local database.");
            }
            catch (Exception ex)
            {
                // Log but don't crash startup. This happens if the DB hasn't been migrated yet 
                // or if connectivity is physically down.
                Serilog.Log.Error(ex, "Failed to load pending actions in ResilienceService");
            }
        }

        public async Task QueueActionAsync(string entityType, OfflineActionType actionType, string payload)
        {
            var action = new OfflineAction
            {
                EntityType = entityType,
                ActionType = actionType,
                Payload = payload
            };
            
            PendingActions.Add(action);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
            context.Set<OfflineAction>().Add(action);
            await context.SaveChangesAsync();
        }

        public async Task ProcessQueueAsync()
        {
            if (!IsOnline) return;

            var actionsToProcess = new List<OfflineAction>(PendingActions);
            foreach (var action in actionsToProcess)
            {
                try
                {
                    // Logic to send to API / MediatR
                    await Task.Delay(500); // Simulate sync
                    
                    PendingActions.Remove(action);

                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
                    var dbAction = await context.Set<OfflineAction>().FindAsync(action.Id);
                    if (dbAction != null)
                    {
                        context.Set<OfflineAction>().Remove(dbAction);
                        await context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    action.RetryCount++;
                    action.LastError = ex.Message;
                    
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
                    var dbAction = await context.Set<OfflineAction>().FindAsync(action.Id);
                    if (dbAction != null)
                    {
                        dbAction.RetryCount = action.RetryCount;
                        dbAction.LastError = action.LastError;
                        await context.SaveChangesAsync();
                    }

                    if (action.RetryCount > 3)
                    {
                        // Handle critical failure / Conflict
                    }
                }
            }
        }
    }
}
