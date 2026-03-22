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
        private readonly string _backupPath;

        public SecureStorageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var luxuryaFolder = Path.Combine(appData, "Luxurya");
            if (!Directory.Exists(luxuryaFolder)) Directory.CreateDirectory(luxuryaFolder);
            _filePath = Path.Combine(luxuryaFolder, "secrets.dat");

            var recoveryFolder = Path.Combine(luxuryaFolder, "recovery");
            if (!Directory.Exists(recoveryFolder)) Directory.CreateDirectory(recoveryFolder);
            _backupPath = Path.Combine(recoveryFolder, "secrets.bak");
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
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[SecureStorage] Primary secrets.dat decrypt failed — attempting backup recovery");

                // Try backup location before giving up
                try
                {
                    if (File.Exists(_backupPath))
                    {
                        byte[] backupBytes = File.ReadAllBytes(_backupPath);
                        byte[] recovered = ProtectedData.Unprotect(backupBytes, null, DataProtectionScope.CurrentUser);
                        Serilog.Log.Warning("[SecureStorage] Successfully recovered from backup — restoring primary");

                        // Restore primary from backup
                        File.WriteAllBytes(_filePath, backupBytes);
                        
                        string json = System.Text.Encoding.UTF8.GetString(recovered);
                        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                    }
                }
                catch (Exception backupEx)
                {
                    Serilog.Log.Error(backupEx, "[SecureStorage] Backup recovery also failed");
                }

                // Both failed — rename primary, app will regenerate on next launch
                if (File.Exists(_filePath))
                {
                    var corruptPath = _filePath + ".corrupt." + DateTime.UtcNow.Ticks;
                    try { File.Move(_filePath, corruptPath); }
                    catch { /* if rename fails just leave the file */ }
                }
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
                
                // Write primary
                File.WriteAllBytes(_filePath, encryptedData);

                // Write backup — separate write to ensure both are tried independently
                try 
                {
                    File.WriteAllBytes(_backupPath, encryptedData);
                }
                catch (Exception backupEx)
                {
                    Serilog.Log.Warning(backupEx, "[SecureStorage] Failed to write backup secrets.dat");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[SecureStorage] Failed to save secure storage");
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
