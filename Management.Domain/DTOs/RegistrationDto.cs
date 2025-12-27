using System;
using Management.Domain.Enums; // Required for RegistrationStatus

namespace Management.Domain.DTOs
{
    public class RegistrationDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Source { get; set; } // "Web", "Walk-in"

        public DateTime CreatedAt { get; set; }

        // FIX: Added missing Status property
        public RegistrationStatus Status { get; set; }

        public string PreferredPlanName { get; set; }
        public DateTime? PreferredStartDate { get; set; }
        public string Notes { get; set; }
    }
}