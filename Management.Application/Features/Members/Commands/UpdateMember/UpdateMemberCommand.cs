using Management.Application.Abstractions.Messaging;
using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Members.Commands.UpdateMember
{
    public record UpdateMemberCommand(MemberDto Member) : IRequest<Result>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Members" };
    }
}
