using Management.Domain.DTOs;
using Management.Domain.Enums;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Members.Queries.GetMembers
{
    public record GetMembersQuery(string SearchText, MemberFilterType Filter) : IRequest<List<MemberDto>>;
}
