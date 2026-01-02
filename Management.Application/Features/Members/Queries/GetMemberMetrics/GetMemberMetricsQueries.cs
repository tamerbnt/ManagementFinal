using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Members.Queries.GetMemberMetrics
{
    public record GetActiveMemberCountQuery() : IRequest<Result<int>>;
    public record GetExpiringMemberCountQuery() : IRequest<Result<int>>;
}
