using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DecryptLease
{
    public class LicenseLease
    {
        public string HardwareId { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public string Signature { get; set; } = string.Empty;
    }

    class Program
    {
        private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("GymManagement-2026-Entropy");

        static void Main(string[] args)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string path = Path.Combine(appData, "Luxurya", "license.lease");
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }

            try
            {
                string encrypted = File.ReadAllText(path);
                byte[] data = Convert.FromBase64String(encrypted);
                byte[] decryptedData = ProtectedData.Unprotect(data, OptionalEntropy, DataProtectionScope.LocalMachine);
                string json = Encoding.UTF8.GetString(decryptedData);

                var lease = JsonSerializer.Deserialize<LicenseLease>(json);
                Console.WriteLine($"STORED_HW_ID: {lease.HardwareId}");
                Console.WriteLine($"EXPIRY: {lease.ExpiryDate}");
                Console.WriteLine($"IS_EXPIRED: {DateTime.UtcNow > lease.ExpiryDate}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
