using System;

namespace Management.Domain.Models
{
    public class LicenseLease
    {
        public string HardwareId { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public string Signature { get; set; } = string.Empty; // SHA256 of (HardwareId + ExpiryDate + SecretSalt)
        
        // Phase 2 Additions for Lifetime Cash Licensing & Multi-Tenant Support
        public bool IsLifetime { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? BranchId { get; set; }
        public int PlanRank { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public bool CrossBranchReports { get; set; }
        public bool SharedMembers { get; set; }

        public bool IsValid(string currentHardwareId)
        {
            if (HardwareId != currentHardwareId) return false;
            if (IsLifetime) return true; // Lifetime perpetual paid licenses never expire
            return DateTime.UtcNow < ExpiryDate;
        }
    }
}
