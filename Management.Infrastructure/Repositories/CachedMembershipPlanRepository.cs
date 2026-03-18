using Management.Domain.Interfaces;
using Management.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Infrastructure.Repositories
{
    /// <summary>
    /// Decorator for IMembershipPlanRepository that adds caching capabilities.
    /// The cache is keyed by facilityId to ensure data isolation.
    /// </summary>
    public class CachedMembershipPlanRepository : IMembershipPlanRepository
    {
        private readonly IMembershipPlanRepository _innerRepository;
        private readonly IMemoryCache _cache;
        private const string PlansCacheKeyPrefix = "MembershipPlans_Active_";

        public CachedMembershipPlanRepository(IMembershipPlanRepository innerRepository, IMemoryCache cache)
        {
            _innerRepository = innerRepository;
            _cache = cache;
        }

        public Task<MembershipPlan?> GetByIdAsync(Guid id, Guid? facilityId = null) => _innerRepository.GetByIdAsync(id, facilityId);

        public Task<IEnumerable<MembershipPlan>> GetAllAsync() => _innerRepository.GetAllAsync();

        public async Task<MembershipPlan> AddAsync(MembershipPlan entity, bool saveChanges = true) 
        {
            var result = await _innerRepository.AddAsync(entity, saveChanges);
            InvalidateCache(entity.FacilityId);
            return result;
        }

        public async Task UpdateAsync(MembershipPlan entity, bool saveChanges = true)
        {
            await _innerRepository.UpdateAsync(entity, saveChanges);
            InvalidateCache(entity.FacilityId);
        }

        public async Task DeleteAsync(Guid id, bool saveChanges = true)
        {
            await _innerRepository.DeleteAsync(id, saveChanges);
            // We don't have the facilityId here easily unless we load the entity first,
            // so we'll clear all as a fallback if specific invalidation is preferred elsewhere.
            InvalidateCache(null); 
        }

        public async Task DeleteAsync(MembershipPlan entity)
        {
            await _innerRepository.DeleteAsync(entity.Id);
            InvalidateCache(entity.FacilityId);
        }

        public async Task<IEnumerable<MembershipPlan>> GetActivePlansAsync(Guid? facilityId = null, bool activeOnly = true)
        {
            var cacheKey = PlansCacheKeyPrefix + (facilityId?.ToString() ?? "all") + (activeOnly ? "_active" : "_all");
            if (_cache.TryGetValue(cacheKey, out IEnumerable<MembershipPlan> cachedPlans))
            {
                return cachedPlans;
            }

            var plans = await _innerRepository.GetActivePlansAsync(facilityId, activeOnly);
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));

            _cache.Set(cacheKey, plans, cacheOptions);

            return plans;
        }

        private void InvalidateCache(Guid? facilityId)
        {
            // Invalidate both the specific key and the global key
            // Now also consider the activeOnly parameter in cache keys
            var specificKeyPrefix = PlansCacheKeyPrefix + (facilityId?.ToString() ?? "all");
            _cache.Remove(specificKeyPrefix + "_active");
            _cache.Remove(specificKeyPrefix + "_all");

            // Also invalidate the global "all facilities" keys if facilityId was null or if we want to be thorough
            if (facilityId != null) // Only if a specific facility was updated, also clear the "all facilities" cache
            {
                _cache.Remove(PlansCacheKeyPrefix + "all" + "_active");
                _cache.Remove(PlansCacheKeyPrefix + "all" + "_all");
            }
        }
    }
}
