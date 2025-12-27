using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class MemberDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string CardId { get; set; }

        // FIX 1: Renamed 'AvatarUrl' to 'ProfileImageUrl' to match Service/ViewModel usage
        public string ProfileImageUrl { get; set; }

        public MemberStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpirationDate { get; set; }

        // FIX 2: Renamed 'PlanName' to 'MembershipPlanName' to match Service usage
        public string MembershipPlanName { get; set; }

        public string EmergencyContactName { get; set; }
        public string EmergencyContactPhone { get; set; }

        // FIX 3: Added missing 'Notes' property required by Service mapping
        public string Notes { get; set; }
    }
}