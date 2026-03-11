using System;

namespace Management.Domain.Models
{
    public class LicenseLease
    {
        public string HardwareId { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public string Signature { get; set; } = string.Empty; // SHA256 of (HardwareId + ExpiryDate + SecretSalt)
        
        public bool IsValid(string currentHardwareId)
        {
            return HardwareId == currentHardwareId && DateTime.UtcNow < ExpiryDate;
        }
    }
}
