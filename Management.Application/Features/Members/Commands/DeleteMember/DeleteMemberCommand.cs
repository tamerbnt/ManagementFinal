using Management.Application.Abstractions.Messaging;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Members.Commands.DeleteMember
{
    public record DeleteMemberCommand(Guid Id) : IRequest<Result>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Members" };
    }
}
