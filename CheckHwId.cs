using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace DiagnosticApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var motherboard = GetMotherboardId();
                var cpu = GetCpuId();
                
                var rawId = $"HW-STB-{motherboard}-{cpu}";
                var hashedId = HashString(rawId);
                Console.WriteLine($"MOTHERBOARD: {motherboard}");
                Console.WriteLine($"CPU: {cpu}");
                Console.WriteLine($"HARDWARE_ID: {hashedId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static string GetMotherboardId()
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                return obj["SerialNumber"]?.ToString() ?? "UNKNOWN_MB";
            }
            return "UNKNOWN_MB";
        }

        static string GetCpuId()
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                return obj["ProcessorId"]?.ToString() ?? "UNKNOWN_CPU";
            }
            return "UNKNOWN_CPU";
        }

        static string HashString(string input)
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
