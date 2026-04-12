using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class Promotion : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PromotionTargetType TargetType { get; set; }
        public Guid TargetId { get; set; }
        public Money? PromotionPrice { get; set; }
        public decimal? DiscountPercentage { get; set; }

        // Criteria
        public Gender? CriteriaGender { get; set; }
        public Guid? CriteriaMemberPlanId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }

        public Promotion() { } // EF Core and manual initialization

        private Promotion(
            Guid id,
            string name,
            PromotionTargetType targetType,
            Guid targetId,
            Money promotionPrice,
            decimal? discountPercentage,
            Gender? criteriaGender,
            Guid? criteriaMemberPlanId,
            DateTime startDate,
            DateTime endDate) : base(id)
        {
            Name = name;
            TargetType = targetType;
            TargetId = targetId;
            PromotionPrice = promotionPrice;
            DiscountPercentage = discountPercentage;
            CriteriaGender = criteriaGender;
            CriteriaMemberPlanId = criteriaMemberPlanId;
            StartDate = startDate;
            EndDate = endDate;
            IsActive = true;
        }

        public static Result<Promotion> Create(
            string name,
            PromotionTargetType targetType,
            Guid targetId,
            Money promotionPrice,
            decimal? discountPercentage,
            Gender? criteriaGender,
            Guid? criteriaMemberPlanId,
            DateTime startDate,
            DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<Promotion>(new Error("Promotion.EmptyName", "Promotion name is required."));

            if (startDate >= endDate)
                return Result.Failure<Promotion>(new Error("Promotion.InvalidDates", "Start date must be before end date."));

            var promotion = new Promotion(
                Guid.NewGuid(),
                name,
                targetType,
                targetId,
                promotionPrice,
                discountPercentage,
                criteriaGender,
                criteriaMemberPlanId,
                startDate,
                endDate);

            return Result.Success(promotion);
        }

        public void UpdateDetails(
            string name,
            Money promotionPrice,
            decimal? discountPercentage,
            Gender? criteriaGender,
            Guid? criteriaMemberPlanId,
            DateTime startDate,
            DateTime endDate,
            bool isActive)
        {
            Name = name;
            PromotionPrice = promotionPrice;
            DiscountPercentage = discountPercentage;
            CriteriaGender = criteriaGender;
            CriteriaMemberPlanId = criteriaMemberPlanId;
            StartDate = startDate;
            EndDate = endDate;
            IsActive = isActive;
            UpdateTimestamp();
        }

        public bool IsCurrentlyActive => IsActive && DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;

        public void Deactivate() => IsActive = false;
        public void Activate() => IsActive = true;
    }
}
