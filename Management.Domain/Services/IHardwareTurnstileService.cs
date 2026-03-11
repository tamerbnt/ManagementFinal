using System;
using System.Threading.Tasks;
using Management.Domain.Events;

namespace Management.Domain.Services
{
    public interface IHardwareTurnstileService : IDisposable
    {
        event EventHandler<TurnstileScanEventArgs>? CardScanned;
        event Action<bool>? ConnectionStatusChanged;

        bool IsSdkAvailable { get; }
        bool IsConnected { get; }
        string DeviceIp { get; }
        int DevicePort { get; }

        Task<bool> ConnectAsync(string ip, int port);
        Task DisconnectAsync();
        Task<bool> OpenGateAsync();
        Task<bool> PingAsync();
        void StartMonitoring();
    }
}
