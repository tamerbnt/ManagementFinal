using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Management.Domain.Services;
using Management.Infrastructure.Integrations.Supabase.Models;

namespace Management.Infrastructure.Services
{
    public class SupabaseRealtimeService : BackgroundService
    {
        private readonly Supabase.Client _supabase;
        private readonly ISyncEventDispatcher _dispatcher;
        private readonly IFacilityContextService _facilityContext;
        private readonly ILogger<SupabaseRealtimeService> _logger;
        private RealtimeChannel? _channel;
        private readonly SemaphoreSlim _subscriptionSemaphore = new(1, 1);
        
        public event Action<SupabaseRegistrationRequest>? OnWebsiteRegistrationRequestReceived;

        public SupabaseRealtimeService(
            Supabase.Client supabase, 
            ISyncEventDispatcher dispatcher,
            IFacilityContextService facilityContext,
            ILogger<SupabaseRealtimeService> logger)
        {
            _supabase = supabase;
            _dispatcher = dispatcher;
            _facilityContext = facilityContext;
            _logger = logger;
        }

        // Cache reflection properties to improve performance
        private System.Reflection.PropertyInfo? _tableProperty;
        private System.Reflection.PropertyInfo? _tableNameProperty;
        private System.Reflection.PropertyInfo? _eventProperty;
        private System.Reflection.PropertyInfo? _eventTypeProperty;
        private System.Reflection.PropertyInfo? _payloadProperty;
        private System.Reflection.PropertyInfo? _dataProperty;
        private System.Reflection.PropertyInfo? _recordProperty;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Initializing Supabase Realtime listeners...");
                await _supabase.InitializeAsync();
                await _supabase.Realtime.ConnectAsync();

                // Listen for facility changes to refresh subscriptions
                _facilityContext.FacilityChanged += async (facility) => 
                {
                    _logger.LogInformation("Facility change detected ({Facility}). Refreshing realtime subscriptions...", facility);
                    await RefreshSubscriptionsAsync(stoppingToken);
                };

                // Initial subscription
                await RefreshSubscriptionsAsync(stoppingToken);

                // Keep service alive until cancellation
                try 
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    if (_channel != null) _channel.Unsubscribe();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Supabase Realtime Service");
            }
        }

        private async Task RefreshSubscriptionsAsync(CancellationToken ct)
        {
            if (!await _subscriptionSemaphore.WaitAsync(100, ct)) return;
            
            try
            {
                // Graceful Handoff: Tearing down old channel
                if (_channel != null)
                {
                    _logger.LogDebug("Tearing down existing realtime channel...");
                    _channel.Unsubscribe();
                    _channel = null;
                    // Small delay to allow Supabase/Websocket to process the unsubscribe
                    await Task.Delay(500, ct);
                }

                var facilityId = _facilityContext.CurrentFacilityId;
                if (facilityId == Guid.Empty)
                {
                    _logger.LogWarning("Realtime: No valid facility context. Subscriptions deferred.");
                    return;
                }

                _logger.LogInformation("Establishing Realtime subscriptions for Facility: {FacilityId}", facilityId);

                _channel = _supabase.Realtime.Channel($"db-sync-{facilityId}");

                // Listen for changes ONLY for this facility
                var memberOptions = new PostgresChangesOptions("public", "members")
                {
                    Filter = $"facility_id=eq.{facilityId}"
                };
                var registrationOptions = new PostgresChangesOptions("public", "registrations")
                {
                    Filter = $"facility_id=eq.{facilityId}"
                };
                var registrationRequestsOptions = new PostgresChangesOptions("public", "registration_requests");

                _channel.Register(memberOptions);
                _channel.Register(registrationOptions);
                _channel.Register(registrationRequestsOptions);


                _channel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.All, (sender, change) =>
                {
                    try
                    {
                        var type = change.GetType();

                        // Lazy load reflection properties
                        if (_tableProperty == null)
                        {
                            _tableProperty = type.GetProperty("Table");
                            _tableNameProperty = type.GetProperty("TableName");
                            _eventProperty = type.GetProperty("Event");
                            _eventTypeProperty = type.GetProperty("EventType");
                            _payloadProperty = type.GetProperty("Payload");
                            
                            _logger.LogInformation("Realtime reflection cache initialized.");
                        }

                        var table = _tableProperty?.GetValue(change) as string 
                                    ?? _tableNameProperty?.GetValue(change) as string 
                                    ?? "unknown";
                                    
                        var eventType = _eventProperty?.GetValue(change) as string 
                                        ?? _eventTypeProperty?.GetValue(change) as string 
                                        ?? "unknown";

                        _logger.LogInformation($"Realtime change detected: {table} - {eventType}");
                        
                        string data = "{}";
                        var payloadProp = _payloadProperty?.GetValue(change);
                        
                        if (payloadProp != null)
                        {
                            if (_dataProperty == null)
                            {
                                var payloadType = payloadProp.GetType();
                                _dataProperty = payloadType.GetProperty("Data");
                                if (_dataProperty != null)
                                {
                                    var dataType = _dataProperty.PropertyType;
                                    _recordProperty = dataType.GetProperty("Record");
                                }
                            }

                            var dataObj = _dataProperty?.GetValue(payloadProp);
                            var recordObj = _recordProperty?.GetValue(dataObj);
                            
                            if (recordObj != null)
                            {
                                data = JsonSerializer.Serialize(recordObj);
                            }
                        }

                        _dispatcher.DispatchAsync(table, eventType, data);

                        // Special handling for website registration requests (Realtime feed)
                        if (table == "registration_requests" && (eventType == "INSERT" || eventType == "insert"))
                        {
                            try
                            {
                                var request = JsonSerializer.Deserialize<SupabaseRegistrationRequest>(data);
                                if (request != null)
                                    OnWebsiteRegistrationRequestReceived?.Invoke(request);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[Realtime] Failed to deserialize website registration request");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Realtime callback");
                    }
                });

                await _channel.Subscribe();
                _logger.LogInformation("Supabase Realtime subscription active for {FacilityId}.", facilityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh realtime subscriptions");
            }
            finally
            {
                _subscriptionSemaphore.Release();
            }
        }
    }
}
