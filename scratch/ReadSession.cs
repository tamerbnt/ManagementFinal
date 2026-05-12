using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Diagnostic
{
    class Program
    {
        static void Main()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GymManagement"
                );
                var sessionFilePath = Path.Combine(appDataPath, "session.dat");
                var entropy = Encoding.UTF8.GetBytes("GymManagement_Session_Salt_2024");

                if (!File.Exists(sessionFilePath))
                {
                    Console.WriteLine("ERROR: session.dat not found at " + sessionFilePath);
                    return;
                }

                var encryptedBytes = File.ReadAllBytes(sessionFilePath);
                var bytes = ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(bytes);
                
                Console.WriteLine("--- SESSION DATA ---");
                Console.WriteLine(json);
                Console.WriteLine("--------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
