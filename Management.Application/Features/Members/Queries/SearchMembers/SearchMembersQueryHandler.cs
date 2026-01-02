using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
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

        public SearchMembersQueryHandler(IMemberRepository memberRepository, IMembershipPlanRepository planRepository)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
        }

        public async Task<Result<PagedResult<MemberDto>>> Handle(SearchMembersQuery request, CancellationToken cancellationToken)
        {
            var filterType = request.Request.FilterType;
            var statusFilter = MapFilterToStatus(filterType);

            // 1. Fetch filtered list from Repository
            var allMatches = (await _memberRepository.SearchAsync(request.Request.SearchTerm, statusFilter)).ToList();

            // 2. Filter logic for "Expiring" (Business Logic)
            if (filterType == MemberFilterType.Expiring)
            {
                var threshold = DateTime.UtcNow.AddDays(7);
                allMatches = allMatches.Where(m => m.Status == MemberStatus.Active && m.ExpirationDate <= threshold).ToList();
            }

            var totalCount = allMatches.Count;

            // 3. Apply Pagination
            var pagedEntities = allMatches
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // 4. Map to DTOs
            var dtos = new List<MemberDto>();
            foreach (var entity in pagedEntities)
            {
                dtos.Add(await MapToDto(entity));
            }

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
