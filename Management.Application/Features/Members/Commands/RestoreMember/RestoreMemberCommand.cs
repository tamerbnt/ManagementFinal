using Management.Application.Abstractions.Messaging;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Members.Commands.RestoreMember
{
    public record RestoreMemberCommand(Guid Id) : IRequest<Result>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Members" };
    }
}
