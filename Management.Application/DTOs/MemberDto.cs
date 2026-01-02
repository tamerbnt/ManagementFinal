using System;
using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record MemberDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string CardId { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public MemberStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string MembershipPlanName { get; set; } = string.Empty;
        public Guid? MembershipPlanId { get; set; }
        public string EmergencyContactName { get; set; } = string.Empty;
        public string EmergencyContactPhone { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}