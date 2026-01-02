using Management.Application.Abstractions.Messaging;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Members.Commands.RenewMembership
{
    // Supporting batch renewal to match legacy parity, but can also be single.
    public record RenewMembershipCommand(List<Guid> MemberIds) : IRequest<Result>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Members" };
    }
}
