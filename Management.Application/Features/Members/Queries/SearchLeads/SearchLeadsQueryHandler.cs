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

namespace Management.Application.Features.Members.Queries.SearchLeads
{
    public class SearchLeadsQueryHandler : IRequestHandler<SearchLeadsQuery, Result<List<MemberDto>>>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IFacilityContextService _facilityContext;

        public SearchLeadsQueryHandler(IMemberRepository memberRepository, IFacilityContextService facilityContext)
        {
            _memberRepository = memberRepository;
            _facilityContext = facilityContext;
        }

        public async Task<Result<List<MemberDto>>> Handle(SearchLeadsQuery request, CancellationToken cancellationToken)
        {
            var facilityId = _facilityContext.CurrentFacilityId == Guid.Empty ? (Guid?)null : _facilityContext.CurrentFacilityId;
            
            // Search for leads by name or phone
            var (pagedItems, _) = await _memberRepository.SearchPagedAsync(
                request.Query,
                facilityId,
                1,
                20,
                MemberFilterType.All,
                MemberStatus.Lead);

            var dtos = pagedItems.Select(entity => new MemberDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email?.Value ?? string.Empty,
                PhoneNumber = entity.PhoneNumber?.Value ?? string.Empty,
                CardId = entity.CardId,
                Status = entity.Status,
                StartDate = entity.StartDate,
                ExpirationDate = entity.ExpirationDate,
                Source = entity.Source ?? "Walk-In",
                IsDeleted = entity.IsDeleted
            }).ToList();

            return Result.Success(dtos);
        }
    }
}
