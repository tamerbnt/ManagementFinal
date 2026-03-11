using Management.Application.Abstractions.Messaging;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Products.Commands.CreateProduct
{
    public record CreateProductCommand(ProductDto Product, Guid? FacilityId = null) : IRequest<Result<Guid>>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Manage Inventory" };
    }
}
