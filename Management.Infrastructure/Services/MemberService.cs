using Management.Application.Features.Members.Queries.GetMember;
using Management.Application.Services;
using Management.Application.Features.Members.Queries.SearchMembers;
using Management.Application.Features.Members.Queries.SearchLeads;
using Management.Application.Features.Members.Queries.GetMemberMetrics;
using Management.Application.Features.Members.Commands.CreateMember;
using Management.Application.Features.Members.Commands.UpdateMember;
using Management.Application.Features.Members.Commands.DeleteMember;
using Management.Application.Features.Members.Commands.RestoreMember;
using Management.Application.Features.Members.Commands.RenewMembership;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Enums;

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
 
        public async Task<Result<List<MemberDto>>> SearchLeadAsync(Guid facilityId, string query)
        {
            // We need to create SearchLeadsQuery
            return await _sender.Send(new SearchLeadsQuery(query));
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

        public async Task<Result> RestoreMembersAsync(Guid facilityId, List<Guid> ids)
        {
            foreach (var id in ids)
            {
                var result = await _sender.Send(new RestoreMemberCommand(id));
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

        public async Task<Result<List<MemberDto>>> GetRecentlyExpiredMembersAsync(Guid facilityId, int daysBack)
        {
            var request = new MemberSearchRequest(
                SearchTerm: string.Empty,
                FilterType: MemberFilterType.Expired,
                EndDate: DateTime.UtcNow,
                StartDate: DateTime.UtcNow.AddDays(-daysBack)
            );

            var result = await SearchMembersAsync(facilityId, request, 1, 50);
            if (result.IsFailure) return Result.Failure<List<MemberDto>>(result.Error);

            return Result.Success(result.Value.Items.ToList());
        }
    }
}
