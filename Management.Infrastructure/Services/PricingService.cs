using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Models;
using Management.Domain.Services;
using Management.Domain.ValueObjects;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services;

public class PricingService : IPricingService
{
    private readonly AppDbContext _context;
        private static readonly ConcurrentDictionary<Guid, (List<Promotion> Promos, DateTime Expiry)> _cache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public PricingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PricingResult> CalculateEffectivePriceAsync(Guid facilityId, Guid targetId, Money basePrice, Management.Domain.Enums.Gender? gender = null, Guid? currentPlanId = null)
        {
            var promotions = await GetActivePromotionsAsync(facilityId);
            return CalculatePriceInternal(promotions, targetId, basePrice, gender, currentPlanId);
        }

        public async Task<IDictionary<Guid, PricingResult>> CalculateBatchPricesAsync(
            Guid facilityId, 
            IEnumerable<(Guid Id, Money Price)> items, 
            Management.Domain.Enums.Gender? gender = null, 
            Guid? currentPlanId = null)
        {
            var promotions = await GetActivePromotionsAsync(facilityId);
            var results = new Dictionary<Guid, PricingResult>();

            foreach (var item in items)
            {
                results[item.Id] = CalculatePriceInternal(promotions, item.Id, item.Price, gender, currentPlanId);
            }

            return results;
        }

        private PricingResult CalculatePriceInternal(
            List<Promotion> promotions, 
            Guid targetId, 
            Money basePrice, 
            Management.Domain.Enums.Gender? gender, 
            Guid? currentPlanId)
        {
            // Filter promotions that target this item and match criteria
            var matchingPromotions = promotions
                .Where(p => p.TargetId == targetId && IsMatch(p, gender, currentPlanId))
                .ToList();

            if (!matchingPromotions.Any())
                return PricingResult.Default(basePrice);

            // Calculate effective price for each matching promotion and pick the best (lowest)
            PricingResult bestResult = PricingResult.Default(basePrice);

            foreach (var promo in matchingPromotions)
            {
                decimal effectiveAmount = basePrice.Amount;

                if (promo.PromotionPrice != null && promo.PromotionPrice.Amount > 0)
                {
                    effectiveAmount = promo.PromotionPrice.Amount;
                }
                else if (promo.DiscountPercentage.HasValue)
                {
                    var discount = basePrice.Amount * (promo.DiscountPercentage.Value / 100);
                    effectiveAmount = basePrice.Amount - discount;
                }

                // Senior Dev Rule: Round to 2 decimal places (or based on currency rules)
                effectiveAmount = Math.Round(effectiveAmount, 2);

                if (effectiveAmount < bestResult.EffectivePrice.Amount)
                {
                    bestResult = new PricingResult
                    {
                        EffectivePrice = new Money(effectiveAmount, basePrice.Currency),
                        OriginalPrice = basePrice,
                        DiscountAmount = new Money(basePrice.Amount - effectiveAmount, basePrice.Currency),
                        AppliedPromotionName = promo.Name
                    };
                }
            }

            return bestResult;
        }

        private bool IsMatch(Promotion promo, Management.Domain.Enums.Gender? gender, Guid? currentPlanId)
        {
            // If promo has no criteria, everyone matches
            if (promo.CriteriaGender == null && promo.CriteriaMemberPlanId == null)
                return true;

            // Strict AND Targeting (Senior Dev Critique addressed)
            bool genderMatch = promo.CriteriaGender == null || gender == promo.CriteriaGender;
            bool planMatch = promo.CriteriaMemberPlanId == null || currentPlanId == promo.CriteriaMemberPlanId;

            return genderMatch && planMatch;
        }

        private async Task<List<Promotion>> GetActivePromotionsAsync(Guid facilityId)
        {
            if (_cache.TryGetValue(facilityId, out var cached) && cached.Expiry > DateTime.UtcNow)
            {
                return cached.Promos;
            }

            var now = DateTime.UtcNow;
            var promos = await _context.Promotions
                .Where(p => p.FacilityId == facilityId && p.IsActive && p.StartDate <= now && p.EndDate >= now)
                .AsNoTracking()
                .ToListAsync();

            _cache[facilityId] = (promos, DateTime.UtcNow.Add(CacheDuration));
            return promos;
        }

        public void InvalidateCache(Guid facilityId)
        {
            _cache.TryRemove(facilityId, out _);
        }
    }
