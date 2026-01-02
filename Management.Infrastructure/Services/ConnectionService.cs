using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Management.Domain.Interfaces;
using Management.Domain.Services;

namespace Management.Infrastructure.Services
{
    public class ConnectionService : IConnectionService, IDisposable
    {
        // Event required by MainViewModel
        public event Action<bool>? ConnectionStatusChanged;

        // Configuration
        private const string PingTarget = "8.8.8.8"; // Google DNS
        private const int PingTimeout = 2000; // 2 seconds

        public ConnectionService()
        {
            // Hook into OS-level network changes (Wifi connect/disconnect)
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            // Initial check on startup
            _ = CheckAndNotifyAsync();
        }

        private async void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            // OS says network changed, verify actual internet access
            await CheckAndNotifyAsync();
        }

        private async Task CheckAndNotifyAsync()
        {
            bool isConnected = await IsInternetAvailableAsync();

            // Dispatch to UI thread if necessary, though ViewModel usually handles marshaling.
            // Invoking safely.
            ConnectionStatusChanged?.Invoke(isConnected);
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

        public void Dispose()
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        }
    }
}