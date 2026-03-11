using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Management.Application.Interfaces;

namespace Management.Infrastructure.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly ISecurityService _securityService;
        private readonly string _basePath;

        public ConfigurationService(ISecurityService securityService)
        {
            _securityService = securityService;
            _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ManagementApp");
        }

        public async Task SaveConfigAsync<T>(T config, string filename)
        {
            try
            {
                string json = JsonSerializer.Serialize(config);
                string encrypted = _securityService.Encrypt(json);

                if (!Directory.Exists(_basePath))
                {
                    Directory.CreateDirectory(_basePath);
                }

                string fullPath = Path.Combine(_basePath, filename);
                string tempPath = fullPath + ".tmp";

                // Atomic Save Pattern:
                // 1. Write to .tmp file
                await File.WriteAllTextAsync(tempPath, encrypted);

                // 2. Move .tmp to actual file (atomic replace)
                File.Move(tempPath, fullPath, overwrite: true);
            }
            catch (Exception ex)
            {
                // Log or handle save failure
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
                throw;
            }
        }

        public async Task<T?> LoadConfigAsync<T>(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);
            if (!File.Exists(fullPath)) return default;

            try
            {
                string encrypted = await File.ReadAllTextAsync(fullPath);
                string json = _securityService.Decrypt(encrypted);

                if (string.IsNullOrEmpty(json))
                {
                    // Decryption failed (or returned empty)
                    throw new JsonException("Decryption returned empty content.");
                }

                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception)
            {
                // Corrupt Load Handling:
                // Automatically delete the corrupted file and return null (forcing fresh state)
                try
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch
                {
                    // Ignore delete errors
                }

                return default;
            }
        }
    }
}
