using System;

namespace Management.Application.DTOs
{
    public class OnboardingState
    {
        public string LicenseKey { get; set; } = string.Empty;
        public Guid? LicenseId { get; set; }
        public string HardwareId { get; set; } = string.Empty;
        public string AdminFullName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string SelectedCurrency { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string FacilityType { get; set; } = string.Empty; // Gym, Salon, or Restaurant
        public bool IsMasterNode { get; set; }

        // Phase 2 Additions
        public string? VoucherCode { get; set; }
        public string? Category { get; set; } = "pos_inventory";
        public string? BranchName { get; set; }
    }
}
