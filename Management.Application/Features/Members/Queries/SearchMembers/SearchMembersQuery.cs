using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Members.Queries.SearchMembers
{
    public record SearchMembersQuery(MemberSearchRequest Request, int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<MemberDto>>>;
}
