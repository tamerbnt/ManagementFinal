using System;
using System.Threading.Tasks;
using Management.Domain.Events;
using Management.Domain.Services;

namespace Management.Infrastructure.Hardware
{
    public class MockTurnstileService : IHardwareTurnstileService
    {
        public event EventHandler<TurnstileScanEventArgs>? CardScanned;
        public event Action<bool>? ConnectionStatusChanged;

        public bool IsConnected { get; set; }
        public bool IsSdkAvailable => true;
        public string DeviceIp => "127.0.0.1";
        public int DevicePort => 4370;

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            await Task.Delay(500);
            IsConnected = true;
            ConnectionStatusChanged?.Invoke(true);
            return true;
        }

        public async Task DisconnectAsync()
        {
            await Task.Delay(200);
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(false);
        }

        /// <summary>Counter of how many times the gate was successfully opened. Used in tests.</summary>
        public int GateOpenedCount { get; private set; }

        /// <summary>When true, the next call to OpenGateAsync will simulate a connection drop.</summary>
        public bool DropConnectionOnNextOpen { get; set; }

        public async Task<bool> OpenGateAsync()
        {
            if (!IsConnected) return false;

            if (DropConnectionOnNextOpen)
            {
                DropConnectionOnNextOpen = false;
                IsConnected = false;
                ConnectionStatusChanged?.Invoke(false);
                throw new System.IO.IOException("Simulated connection drop during gate open");
            }

            // Reduced from 3000ms → 50ms for test speed
            await Task.Delay(50);
            GateOpenedCount++;
            return true;
        }

        public async Task<bool> PingAsync()
        {
            return IsConnected;
        }

        public void StartMonitoring()
        {
            // Do nothing, mock doesn't need to start background loops
        }

        public void SimulatePhysicalScan(string cardId)
        {
            CardScanned?.Invoke(this, new TurnstileScanEventArgs(cardId, "MockDevice", "MockTransaction", true, 1, DateTime.UtcNow));
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}
