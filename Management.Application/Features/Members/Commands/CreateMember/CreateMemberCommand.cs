using Management.Application.Abstractions.Messaging;
using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Members.Commands.CreateMember
{
    public record CreateMemberCommand(MemberDto Member) : IRequest<Result<Guid>>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Members" };
    }
}
