using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Members.Queries.GetMember
{
    public record GetMemberQuery(Guid Id) : IRequest<Result<MemberDto>>;
}
