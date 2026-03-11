using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Management.Domain.Interfaces;
using Management.Domain.Services;

namespace Management.Infrastructure.Services
{
    public class ConnectionService : Management.Domain.Services.IConnectionService, IDisposable
    {
        public bool IsConnected { get; private set; }
        public bool IsSupabaseReachable { get; private set; }
        public event Action<bool>? ConnectionStatusChanged;
        public event Action<bool>? SupabaseStatusChanged;
        public event EventHandler<bool>? ConnectivityChanged;

        // Configuration
        private const string PingTarget = "8.8.8.8"; // Google DNS
        private const int PingTimeout = 2000; // 2 seconds
        
        private readonly Supabase.Client? _supabase;
        private readonly System.Threading.Timer _supabasePollTimer;
        private bool _lastSupabaseStatus = true; // Assume online to avoid startup noise if already online

        public ConnectionService(Supabase.Client? supabase = null)
        {
            _supabase = supabase;
            
            // Hook into OS-level network changes (Wifi connect/disconnect)
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            // Initial check on startup
            _ = CheckAndNotifyAsync();

            // Start Supabase polling (every 30 seconds)
            _supabasePollTimer = new System.Threading.Timer(async _ => await PollSupabaseAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        }

        private async Task PollSupabaseAsync()
        {
            bool isReachable = await CanReachSupabaseAsync();
            if (isReachable != _lastSupabaseStatus)
            {
                _lastSupabaseStatus = isReachable;
                IsSupabaseReachable = isReachable;
                SupabaseStatusChanged?.Invoke(isReachable);
            }
        }

        private async void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            // OS says network changed, verify actual internet access
            await CheckAndNotifyAsync();
        }

        private async Task CheckAndNotifyAsync()
        {
            bool isConnected = await IsInternetAvailableAsync();
            IsConnected = isConnected;

            // Dispatch to UI thread if necessary, though ViewModel usually handles marshaling.
            // Invoking safely.
            ConnectionStatusChanged?.Invoke(isConnected);
            ConnectivityChanged?.Invoke(this, isConnected);
        }

        /// <summary>
        /// Performs a real-world connectivity test.
        /// NetworkInterface.GetIsNetworkAvailable() only checks if cable/wifi is active.
        /// We need to know if we can reach the cloud (Supabase).
        /// </summary>
        public async Task<bool> IsInternetAvailableAsync()
        {
            // 1. Fast OS Check
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return false;
            }

            // 2. Active Ping Check
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(PingTarget, PingTimeout);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<bool> CanReachSupabaseAsync()
        {
            if (!await IsInternetAvailableAsync())
                return false;
            
            if (_supabase == null)
                return false;
            
            try
            {
                // Quick health check - just verify we can reach Supabase
                // Use a lightweight query to minimize overhead
                await _supabase.From<Management.Infrastructure.Integrations.Supabase.Models.SupabaseProfile>()
                    .Select("id")
                    .Limit(1)
                    .Get();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public bool IsOnline() => IsConnected;

        public void Dispose()
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            _supabasePollTimer?.Dispose();
        }
    }
}
