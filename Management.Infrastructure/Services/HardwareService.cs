using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Management.Infrastructure.Services
{
    public interface IHardwareService
    {
        string GetHardwareId();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class HardwareService : IHardwareService
    {
        private string _cachedId = string.Empty;

        public string GetHardwareId()
        {
            if (!string.IsNullOrEmpty(_cachedId)) return _cachedId;

            try
            {
                var motherboard = GetMotherboardId();
                var cpu = GetCpuId();
                var ram = GetTotalRam();
                
                var rawId = $"HW-{motherboard}-{cpu}-{ram}";
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
