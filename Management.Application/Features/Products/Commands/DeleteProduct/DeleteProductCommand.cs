using Management.Application.Abstractions.Messaging;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Products.Commands.DeleteProduct
{
    public record DeleteProductCommand(Guid Id) : IRequest<Result>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Inventory" };
    }
}
