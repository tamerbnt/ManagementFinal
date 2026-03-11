using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Application.Interfaces
{
    public class DeviceStatus
    {
        public string Name { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public string Type { get; set; } = string.Empty; // "RFID", "Printer", "Turnstile"
        public string LastError { get; set; } = string.Empty;
    }

    public interface IHardwareService
    {
        string GetHardwareId();
        IEnumerable<DeviceStatus> GetDeviceStatuses();
        event Action<DeviceStatus>? DeviceStatusChanged;
        Task<bool> TestDeviceAsync(string deviceType);
        void NotifyStatusChanged(string deviceType, bool isConnected, string? error = null);
    }
}
