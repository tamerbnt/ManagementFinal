using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Management.Infrastructure.Integrations.Supabase
{
    public class CustomFileSessionHandler : IGotrueSessionPersistence<Session>
    {
        private readonly string _filePath;
        private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("GymManagement-2026-Entropy");

        public CustomFileSessionHandler()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "ManagementApp");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "session.bin");
        }

        public void SaveSession(Session session)
        {
            try
            {
                var json = JsonConvert.SerializeObject(session);
                var data = Encoding.UTF8.GetBytes(json);
                var encrypted = ProtectedData.Protect(data, OptionalEntropy, DataProtectionScope.LocalMachine);
                File.WriteAllBytes(_filePath, encrypted);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[CustomFileSessionHandler] Failed to save session.");
            }
        }

        public Session? LoadSession()
        {
            if (!File.Exists(_filePath)) return null;

            try
            {
                var encrypted = File.ReadAllBytes(_filePath);
                var decrypted = ProtectedData.Unprotect(encrypted, OptionalEntropy, DataProtectionScope.LocalMachine);
                var json = Encoding.UTF8.GetString(decrypted);
                return JsonConvert.DeserializeObject<Session>(json);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[CustomFileSessionHandler] Failed to load session.");
                return null;
            }
        }

        public void DestroySession()
        {
            try
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[CustomFileSessionHandler] Failed to destroy session.");
            }
        }
    }
}
