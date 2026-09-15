using System;

namespace Management.Application.DTOs
{
    public class LicenseCheckResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        // Phase 2 Subscription & Device Metadata
        public string Status { get; set; } = string.Empty; // "registered", "unregistered", "active", "trialing", "expired"
        public int PlanRank { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public bool IsLifetime { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? BusinessId { get; set; }
        public Guid? DeviceId { get; set; }
        public string FacilityName { get; set; } = string.Empty;
        public string? FailureReason { get => ErrorMessage; set => ErrorMessage = value; }
        public bool CrossBranchReports { get; set; }
        public bool SharedMembers { get; set; }

        public static LicenseCheckResult Success() => new() { IsValid = true };
        public static LicenseCheckResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
    }
}
