using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class SupabaseRealtimeService : BackgroundService
    {
        private readonly Supabase.Client _supabase;
        private readonly ISyncEventDispatcher _dispatcher;
        private readonly ILogger<SupabaseRealtimeService> _logger;

        public SupabaseRealtimeService(
            Supabase.Client supabase, 
            ISyncEventDispatcher dispatcher,
            ILogger<SupabaseRealtimeService> logger)
        {
            _supabase = supabase;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Initializing Supabase Realtime listeners...");

                await _supabase.InitializeAsync();

                var channel = _supabase.Realtime.Channel("db-sync");

                // Listen for all changes on members and registrations
                channel.Register(new PostgresChangesOptions("public", "members"));
                channel.Register(new PostgresChangesOptions("public", "registrations"));

                channel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.All, (sender, change) =>
                {
                    try
                    {
                        // Use reflection to bypass potential version-specific naming issues in PostgresChangesResponse
                        var type = change.GetType();
                        var table = type.GetProperty("Table")?.GetValue(change) as string 
                                    ?? type.GetProperty("TableName")?.GetValue(change) as string 
                                    ?? "unknown";
                                    
                        var eventType = type.GetProperty("Event")?.GetValue(change) as string 
                                        ?? type.GetProperty("EventType")?.GetValue(change) as string 
                                        ?? "unknown";

                        _logger.LogInformation($"Realtime change detected: {table} - {eventType}");
                        
                        // Default to empty JSON if we can't get the payload easily
                        string data = "{}";
                        var payloadProp = type.GetProperty("Payload")?.GetValue(change);
                        if (payloadProp != null)
                        {
                            // In many versions, Payload has a Data property which has a Record property
                            var dataProp = payloadProp.GetType().GetProperty("Data")?.GetValue(payloadProp);
                            var recordProp = dataProp?.GetType().GetProperty("Record")?.GetValue(dataProp);
                            if (recordProp != null)
                            {
                                data = JsonSerializer.Serialize(recordProp);
                            }
                        }

                        _dispatcher.DispatchAsync(table, eventType, data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Realtime callback");
                    }
                });

                await channel.Subscribe();

                _logger.LogInformation("Supabase Realtime subscription active.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(10000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Supabase Realtime Service");
            }
        }
    }
}
