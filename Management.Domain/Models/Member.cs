using System;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    public class Member : Entity
    {
        // Core Identity
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        // Physical Access
        public string CardId { get; set; } // RFID/NFC Tag ID
        public string ProfileImageUrl { get; set; }

        // Membership Status
        public MemberStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpirationDate { get; set; }

        // Foreign Key to Membership Plan
        public Guid? MembershipPlanId { get; set; }

        // Emergency Info
        public string EmergencyContactName { get; set; }
        public string EmergencyContactPhone { get; set; }

        // Internal Notes
        public string Notes { get; set; }
    }
}