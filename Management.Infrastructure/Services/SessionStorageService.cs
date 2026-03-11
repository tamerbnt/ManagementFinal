using Management.Domain.Models;
using Management.Domain.Services;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace Management.Infrastructure.Services
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class SessionStorageService : Management.Domain.Services.ISessionStorageService
    {
        private readonly ILogger<SessionStorageService> _logger;
        private readonly string _sessionFilePath;
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("GymManagement_Session_Salt_2024");

        public SessionStorageService(ILogger<SessionStorageService> logger)
        {
            _logger = logger;
            
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GymManagement"
            );
            
            Directory.CreateDirectory(appDataPath);
            _sessionFilePath = Path.Combine(appDataPath, "session.dat");
        }

        public async Task SaveSessionAsync(SessionData session)
        {
            try
            {
                var json = JsonSerializer.Serialize(session);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                // Encrypt using Windows DPAPI
                var encryptedBytes = ProtectedData.Protect(bytes, _entropy, DataProtectionScope.CurrentUser);
                
                await File.WriteAllBytesAsync(_sessionFilePath, encryptedBytes);
                _logger.LogInformation("Session saved for user {Email}", session.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session");
                throw;
            }
        }

        public async Task<SessionData?> LoadSessionAsync()
        {
            try
            {
                if (!File.Exists(_sessionFilePath))
                {
                    _logger.LogDebug("No session file found");
                    return null;
                }

                var encryptedBytes = await File.ReadAllBytesAsync(_sessionFilePath);
                var bytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(bytes);
                
                var session = JsonSerializer.Deserialize<SessionData>(json);
                
                if (session == null)
                {
                    _logger.LogWarning("Session file was empty or invalid");
                    return null;
                }

                if (session.IsExpired)
                {
                    _logger.LogInformation("Loaded session is expired");
                    await ClearSessionAsync();
                    return null;
                }

                _logger.LogInformation("Session loaded for user {Email}", session.Email);
                return session;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt session (may be from different user)");
                await ClearSessionAsync();
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session");
                return null;
            }
        }

        public async Task ClearSessionAsync()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                    _logger.LogInformation("Session cleared");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear session");
            }
            
            await Task.CompletedTask;
        }

        public async Task<bool> HasValidSessionAsync()
        {
            var session = await LoadSessionAsync();
            return session != null && !session.IsExpired;
        }
    }
}
