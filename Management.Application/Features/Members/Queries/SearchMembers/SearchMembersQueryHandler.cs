using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Queries.SearchMembers
{
    public class SearchMembersQueryHandler : IRequestHandler<SearchMembersQuery, Result<PagedResult<MemberDto>>>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMembershipPlanRepository _planRepository;
        private readonly IFacilityContextService _facilityContext;

        public SearchMembersQueryHandler(IMemberRepository memberRepository, IMembershipPlanRepository planRepository, IFacilityContextService facilityContext)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
            _facilityContext = facilityContext;
        }

        public async Task<Result<PagedResult<MemberDto>>> Handle(SearchMembersQuery request, CancellationToken cancellationToken)
        {
            var filterType = request.Request.FilterType;
            bool? isActiveFilter = null;
            DateTime? expiringBefore = null;
            
            var now = DateTime.UtcNow;

            if (filterType == MemberFilterType.Active) isActiveFilter = true;
            else if (filterType == MemberFilterType.Expired) isActiveFilter = false;
            else if (filterType == MemberFilterType.Expiring) expiringBefore = now.AddDays(7);

            // 1. Fetch filtered and paged list from Repository (DB-LEVEL)
            var facilityId = _facilityContext.CurrentFacilityId == Guid.Empty ? (Guid?)null : _facilityContext.CurrentFacilityId;
            
            var (pagedItems, totalCount) = await _memberRepository.SearchPagedAsync(
                request.Request.SearchTerm,
                facilityId,
                request.Page,
                request.PageSize,
                MapFilterToStatus(filterType),
                request.Request.Gender,
                request.Request.StartDate,
                request.Request.EndDate,
                isActiveFilter,
                expiringBefore);

            var items = pagedItems.ToList();

            // 4. Map to DTOs with Batch Plan Fetching (Fixes N+1)
            var planIds = items
                .Where(m => m.MembershipPlanId.HasValue)
                .Select(m => m.MembershipPlanId!.Value)
                .Distinct()
                .ToList();

            var plans = new Dictionary<Guid, string>();
            var allPlansList = new List<MembershipPlan>();
            if (planIds.Any())
            {
                // For gyms, fetching all plans is usually faster than complex IN queries if plan count is small.
                // We'll use the repository to get the required plans.
                var allPlans = await _planRepository.GetActivePlansAsync(facilityId);
                allPlansList = allPlans.ToList();
                plans = allPlansList.ToDictionary(p => p.Id, p => p.Name);
            }

            var dtos = items.Select(entity => new MemberDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email.Value,
                PhoneNumber = entity.PhoneNumber.Value,
                CardId = entity.CardId,
                Status = entity.Status,
                StartDate = entity.StartDate,
                ExpirationDate = entity.ExpirationDate,
                ProfileImageUrl = entity.ProfileImageUrl,
                MembershipPlanName = entity.MembershipPlanId.HasValue && plans.TryGetValue(entity.MembershipPlanId.Value, out string? planName) ? planName : "None",
                MembershipPlanId = entity.MembershipPlanId,
                EmergencyContactName = entity.EmergencyContactName,
                EmergencyContactPhone = entity.EmergencyContactPhone?.Value ?? string.Empty,
                Balance = entity.MembershipPlanId.HasValue ? (decimal)(allPlansList.FirstOrDefault(p => p.Id == entity.MembershipPlanId.Value)?.Price.Amount ?? 0) : 0,
                Notes = entity.Notes
            }).ToList();

            var result = new PagedResult<MemberDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = request.Page,
                PageSize = request.PageSize
            };

            return Result.Success(result);
        }

        // Helper duplicates GetMember logic. 
        // In a real app we might use AutoMapper or a shared Mapping Service to avoid this duplication.
        private async Task<MemberDto> MapToDto(Member entity)
        {
            string planName = "None";
            if (entity.MembershipPlanId.HasValue)
            {
                var plan = await _planRepository.GetByIdAsync(entity.MembershipPlanId.Value);
                if (plan != null) planName = plan.Name;
            }

            return new MemberDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email.Value,
                PhoneNumber = entity.PhoneNumber.Value,
                CardId = entity.CardId,
                Status = entity.Status,
                StartDate = entity.StartDate,
                ExpirationDate = entity.ExpirationDate,
                ProfileImageUrl = entity.ProfileImageUrl,
                MembershipPlanName = planName,
                MembershipPlanId = entity.MembershipPlanId,
                EmergencyContactName = entity.EmergencyContactName,
                EmergencyContactPhone = entity.EmergencyContactPhone?.Value ?? string.Empty,
                Notes = entity.Notes
            };
        }

        private MemberStatus? MapFilterToStatus(MemberFilterType filter)
        {
            return filter switch
            {
                MemberFilterType.Active => MemberStatus.Active,
                MemberFilterType.Expired => MemberStatus.Expired,
                MemberFilterType.Expiring => MemberStatus.Active,
                _ => null
            };
        }
    }
}
