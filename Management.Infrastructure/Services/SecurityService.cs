using System;
using System.Security.Cryptography;
using System.Text;
using Management.Application.Interfaces;

namespace Management.Infrastructure.Services
{
    public class SecurityService : ISecurityService
    {
        private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("GymManagement-2026-Entropy");

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                // CRITICAL: Using LocalMachine scope so any user on the machine can decrypt (e.g. Admin installs, Receptionist runs)
                byte[] encryptedData = ProtectedData.Protect(data, OptionalEntropy, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(encryptedData);
            }
            catch
            {
                return string.Empty;
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            try
            {
                byte[] data = Convert.FromBase64String(cipherText);
                byte[] decryptedData = ProtectedData.Unprotect(data, OptionalEntropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch
            {
                return string.Empty;
            }
        }
        public string Hashing(string input)
        {
             if (string.IsNullOrEmpty(input)) return string.Empty;
             using (var sha256 = SHA256.Create()) // FIPS Compliant: Use OS crypto factory
             {
                 var bytes = Encoding.UTF8.GetBytes(input);
                 var hash = sha256.ComputeHash(bytes);
                 return Convert.ToBase64String(hash);
             }
        }
    }
}
