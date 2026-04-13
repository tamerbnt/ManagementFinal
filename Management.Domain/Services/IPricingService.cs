using Management.Domain.Models;
using Management.Domain.ValueObjects;
using System.Threading.Tasks;
using System;

namespace Management.Domain.Services
{
    public interface IPricingService
    {
        /// <summary>
        /// Calculates the effective price for a product, plan, or service given a client's context.
        /// </summary>
        Task<PricingResult> CalculateEffectivePriceAsync(Guid facilityId, Guid targetId, Money basePrice, Management.Domain.Enums.Gender? gender = null, Guid? currentPlanId = null);

        /// <summary>
        /// Calculates effective prices for a batch of items in a single pass.
        /// </summary>
        Task<System.Collections.Generic.IDictionary<Guid, PricingResult>> CalculateBatchPricesAsync(
            Guid facilityId, 
            System.Collections.Generic.IEnumerable<(Guid Id, Money Price)> items, 
            Management.Domain.Enums.Gender? gender = null, 
            Guid? currentPlanId = null);
        
        /// <summary>
        /// Invalidates the promotion cache for a facility.
        /// </summary>
        void InvalidateCache(Guid facilityId);
    }

    public class PricingResult
    {
        public required Money EffectivePrice { get; init; }
        public required Money OriginalPrice { get; init; }
        public required Money DiscountAmount { get; init; }
        public string? AppliedPromotionName { get; init; }
        public bool IsDiscountApplied => DiscountAmount.Amount > 0;

        public static PricingResult Default(Money basePrice) => new()
        {
            EffectivePrice = basePrice,
            OriginalPrice = basePrice,
            DiscountAmount = new Money(0, basePrice.Currency),
            AppliedPromotionName = null
        };
    }
}
