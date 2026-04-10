using System.Collections.Generic;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Members.Queries.SearchLeads
{
    public record SearchLeadsQuery(string Query) : IRequest<Result<List<MemberDto>>>;
}
