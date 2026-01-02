using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public record RegistrationDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public RegistrationStatus Status { get; set; }
        public string PreferredPlanName { get; set; } = string.Empty;
        public Guid? PreferredPlanId { get; set; }
        public DateTime? PreferredStartDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}