using Management.Application.Abstractions.Messaging;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Products.Commands.UpdateProduct
{
    public record UpdateProductCommand(ProductDto Product, Guid? FacilityId = null) : IRequest<Result>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Inventory" };
    }
}
