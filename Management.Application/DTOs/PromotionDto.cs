using System;
using Management.Domain.Enums;

namespace Management.Application.DTOs
{
    public class PromotionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PromotionTargetType TargetType { get; set; }
        public Guid TargetId { get; set; }
        public decimal DiscountValue { get; set; }
        public bool IsPercentage { get; set; }
        public Gender? RequiredGender { get; set; }
        public Guid? RequiredMembershipPlanId { get; set; }
        public string? RequiredMembershipPlanName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }
}
