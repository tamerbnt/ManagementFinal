using Management.Application.Features.Members.Queries.GetMembers;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Application.Features.Members.Queries.GetMembers
{
    public class GetMembersHandler : IRequestHandler<GetMembersQuery, List<MemberDto>>
    {
        private readonly IMemberService _memberService;
        private readonly IFacilityContextService _facilityContext;

        public GetMembersHandler(IMemberService memberService, IFacilityContextService facilityContext)
        {
            _memberService = memberService;
            _facilityContext = facilityContext;
        }

        public async Task<List<MemberDto>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var searchRequest = new MemberSearchRequest(request.SearchText, request.Filter);
            
            var result = await _memberService.SearchMembersAsync(facilityId, searchRequest);
            
            if (result.IsSuccess)
            {
                return result.Value.Items.ToList();
            }

            return new List<MemberDto>();
        }
    }
}
