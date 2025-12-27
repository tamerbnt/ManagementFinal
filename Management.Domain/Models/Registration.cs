using System;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a Lead or Pending Member waiting for approval.
    /// </summary>
    public class Registration : Entity
    {
        // Contact Info
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        // Marketing Info
        public string Source { get; set; } // "Walk-in", "Instagram", etc.

        // Workflow
        public RegistrationStatus Status { get; set; }
        public string Notes { get; set; }

        // Interest
        public Guid? RequestedPlanId { get; set; }
        public DateTime? PreferredStartDate { get; set; }
    }
}