using Management.Application.Features.Members.Queries.GetMember;
using Management.Application.Features.Members.Queries.SearchMembers;
using Management.Application.Features.Members.Queries.GetMemberMetrics;
using Management.Application.Features.Members.Commands.CreateMember;
using Management.Application.Features.Members.Commands.UpdateMember;
using Management.Application.Features.Members.Commands.DeleteMember;
using Management.Application.Features.Members.Commands.RenewMembership;
using Management.Domain.DTOs;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class MemberService : IMemberService
    {
        private readonly ISender _sender;

        public MemberService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result<PagedResult<MemberDto>>> SearchMembersAsync(Guid facilityId, MemberSearchRequest request, int page = 1, int pageSize = 20)
        {
            return await _sender.Send(new SearchMembersQuery(request, page, pageSize));
        }

        public async Task<Result<MemberDto>> GetMemberAsync(Guid facilityId, Guid id)
        {
            return await _sender.Send(new GetMemberQuery(id));
        }

        public async Task<Result<Guid>> CreateMemberAsync(Guid facilityId, MemberDto member)
        {
            return await _sender.Send(new CreateMemberCommand(member));
        }

        public async Task<Result> UpdateMemberAsync(Guid facilityId, MemberDto member)
        {
            return await _sender.Send(new UpdateMemberCommand(member));
        }

        public async Task<Result> DeleteMembersAsync(Guid facilityId, List<Guid> ids)
        {
            foreach (var id in ids)
            {
                var result = await _sender.Send(new DeleteMemberCommand(id));
                if (result.IsFailure) return result;
            }
            return Result.Success();
        }

        public async Task<Result> RenewMembersAsync(Guid facilityId, List<Guid> ids)
        {
            return await _sender.Send(new RenewMembershipCommand(ids));
        }

        public async Task<Result<int>> GetActiveMemberCountAsync(Guid facilityId)
        {
            return await _sender.Send(new GetActiveMemberCountQuery());
        }

        public async Task<Result<int>> GetExpiringMemberCountAsync(Guid facilityId)
        {
            return await _sender.Send(new GetExpiringMemberCountQuery());
        }
    }
}
