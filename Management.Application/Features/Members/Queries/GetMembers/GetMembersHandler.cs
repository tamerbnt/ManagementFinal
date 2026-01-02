using Management.Application.Features.Members.Queries.GetMembers;
using Management.Domain.DTOs;
using Management.Domain.Services;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
