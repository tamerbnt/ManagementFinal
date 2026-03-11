using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Management.Application.Interfaces;

namespace Management.Infrastructure.Services
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class HardwareService : IHardwareService
    {
        private readonly IServiceProvider _serviceProvider;
        private string _cachedId = string.Empty;
        private readonly List<DeviceStatus> _statuses = new();

        public event Action<DeviceStatus>? DeviceStatusChanged;

        public HardwareService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            
            // Initialize static statuses
            _statuses.Add(new DeviceStatus { Name = "RFID Reader", Type = "RFID", IsConnected = false });
            _statuses.Add(new DeviceStatus { Name = "Default Printer", Type = "Printer", IsConnected = true });
            _statuses.Add(new DeviceStatus { Name = "Gym Turnstile", Type = "Turnstile", IsConnected = false });
            _statuses.Add(new DeviceStatus { Name = "Barcode Scanner", Type = "Scanner", IsConnected = true });
        }

        public IEnumerable<DeviceStatus> GetDeviceStatuses() => _statuses;

        public async Task<bool> TestDeviceAsync(string deviceType)
        {
            // Simple simulation/logic for testing
            var status = _statuses.Find(s => s.Type == deviceType);
            if (status == null) return false;

            try
            {
                if (deviceType == "RFID")
                {
                    // Basic check: is port available?
                    return true; 
                }
                else if (deviceType == "Printer")
                {
                    // Check specific printer status via WMI
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT PrinterStatus, DetectedErrorState FROM Win32_Printer WHERE Default = True");
                    
                    foreach (var obj in searcher.Get())
                    {
                        var statusValue = Convert.ToInt32(obj["PrinterStatus"]);
                        var errorValue = Convert.ToInt32(obj["DetectedErrorState"]);

                        // Status 3 = Idle, 4 = Printing, 5 = Warming Up
                        if (statusValue >= 3 && statusValue <= 5 && errorValue == 0)
                        {
                            return true;
                        }
                        
                        status.LastError = $"Status: {statusValue}, Error: {errorValue}";
                        return false;
                    }
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                status.LastError = ex.Message;
                DeviceStatusChanged?.Invoke(status);
                return false;
            }
        }

        public void NotifyStatusChanged(string deviceType, bool isConnected, string? error = null)
        {
            var status = _statuses.Find(s => s.Type == deviceType);
            if (status != null)
            {
                status.IsConnected = isConnected;
                status.LastError = error ?? string.Empty;
                DeviceStatusChanged?.Invoke(status);
            }
        }

        public string GetHardwareId()
        {
            if (!string.IsNullOrEmpty(_cachedId)) return _cachedId;

            try
            {
                var motherboard = GetMotherboardId();
                var cpu = GetCpuId();
                
                var rawId = $"HW-STB-{motherboard}-{cpu}";
                _cachedId = HashString(rawId);
                return _cachedId;
            }
            catch (Exception)
            {
                // Fallback to persistent GUID if WMI fails (e.g., restricted permissions)
                _cachedId = GetOrCreatePersistentGuid();
                return _cachedId;
            }
        }

        private string GetTotalRam()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    return obj["TotalPhysicalMemory"]?.ToString() ?? "0";
                }
            }
            catch
            {
                // Ignore and return default
            }
            return "UNKNOWN_RAM";
        }

        private string GetOrCreatePersistentGuid()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var titanDir = System.IO.Path.Combine(appDataPath, "Titan");
            var deviceIdFile = System.IO.Path.Combine(titanDir, "device.id");

            try
            {
                if (System.IO.File.Exists(deviceIdFile))
                {
                    var storedGuid = System.IO.File.ReadAllText(deviceIdFile).Trim();
                    if (!string.IsNullOrEmpty(storedGuid))
                    {
                        return HashString($"FALLBACK-{storedGuid}");
                    }
                }

                // Generate new GUID and persist it
                System.IO.Directory.CreateDirectory(titanDir);
                var newGuid = Guid.NewGuid().ToString();
                System.IO.File.WriteAllText(deviceIdFile, newGuid);
                return HashString($"FALLBACK-{newGuid}");
            }
            catch
            {
                // Ultimate fallback if file system is also restricted
                return HashString($"ULTIMATE-FALLBACK-{Environment.MachineName}-{Environment.UserName}");
            }
        }

        private string GetMotherboardId()
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                return obj["SerialNumber"]?.ToString() ?? "UNKNOWN_MB";
            }
            return "UNKNOWN_MB";
        }

        private string GetCpuId()
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                return obj["ProcessorId"]?.ToString() ?? "UNKNOWN_CPU";
            }
            return "UNKNOWN_CPU";
        }

        private string HashString(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString().ToUpper();
        }
    }
}
