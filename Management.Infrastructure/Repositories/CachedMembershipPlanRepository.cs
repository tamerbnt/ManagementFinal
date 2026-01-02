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
    /// </summary>
    public class CachedMembershipPlanRepository : IMembershipPlanRepository
    {
        private readonly IMembershipPlanRepository _innerRepository;
        private readonly IMemoryCache _cache;
        private const string PlansCacheKey = "MembershipPlans_Active";

        public CachedMembershipPlanRepository(IMembershipPlanRepository innerRepository, IMemoryCache cache)
        {
            _innerRepository = innerRepository;
            _cache = cache;
        }

        public Task<MembershipPlan> GetByIdAsync(Guid id) => _innerRepository.GetByIdAsync(id);

        public Task<IEnumerable<MembershipPlan>> GetAllAsync() => _innerRepository.GetAllAsync();

        public Task<MembershipPlan> AddAsync(MembershipPlan entity) 
        {
            _cache.Remove(PlansCacheKey);
            return _innerRepository.AddAsync(entity);
        }

        public Task UpdateAsync(MembershipPlan entity)
        {
            _cache.Remove(PlansCacheKey);
            return _innerRepository.UpdateAsync(entity);
        }

        public Task DeleteAsync(Guid id)
        {
            _cache.Remove(PlansCacheKey);
            return _innerRepository.DeleteAsync(id);
        }

        public Task DeleteAsync(MembershipPlan entity)
        {
            _cache.Remove(PlansCacheKey);
            return _innerRepository.DeleteAsync(entity.Id);
        }

        public async Task<IEnumerable<MembershipPlan>> GetActivePlansAsync()
        {
            var plans = await _cache.GetOrCreateAsync(PlansCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return _innerRepository.GetActivePlansAsync();
            });

            return plans ?? [];
        }
    }
}
