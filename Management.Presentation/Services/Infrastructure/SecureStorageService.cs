using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Presentation.Services.Infrastructure
{
    public class SecureStorageService : ISecureStorageService
    {
        private readonly string _filePath;

        public SecureStorageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var luxuryaFolder = Path.Combine(appData, "Luxurya");
                if (!Directory.Exists(luxuryaFolder)) Directory.CreateDirectory(luxuryaFolder);
                _filePath = Path.Combine(luxuryaFolder, "secrets.dat");
        }

        public Task<string?> GetAsync(string key)
        {
            return Task.FromResult(Get(key));
        }

        public string? Get(string key)
        {
            var data = GetAll();
            return data.TryGetValue(key, out var value) ? value : null;
        }

        public Task SetAsync(string key, string value)
        {
            var data = GetAll();
            data[key] = value;
            SaveAll(data);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            var data = GetAll();
            if (data.Remove(key))
            {
                SaveAll(data);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ContainsKeyAsync(string key)
        {
            var data = GetAll();
            return Task.FromResult(data.ContainsKey(key));
        }

        public Task ClearAsync()
        {
            DeleteFile();
            return Task.CompletedTask;
        }

        private Dictionary<string, string> GetAll()
        {
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
                return new Dictionary<string, string>();

            try
            {
                byte[] encryptedData = File.ReadAllBytes(_filePath);
                byte[] decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                string json = System.Text.Encoding.UTF8.GetString(decryptedData);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch (Exception)
            {
                DeleteFile();
                return new Dictionary<string, string>();
            }
        }

        private void SaveAll(Dictionary<string, string> data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data);
                byte[] decryptedData = System.Text.Encoding.UTF8.GetBytes(json);
                byte[] encryptedData = ProtectedData.Protect(decryptedData, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_filePath, encryptedData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save secure storage: {ex.Message}");
            }
        }

        private void DeleteFile()
        {
            if (File.Exists(_filePath))
            {
                try { File.Delete(_filePath); } catch { }
            }
        }
    }
}
