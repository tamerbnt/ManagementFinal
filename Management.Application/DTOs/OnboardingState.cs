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
    }
}
