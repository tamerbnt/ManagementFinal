using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Products.Commands.RestoreProduct
{
    public record RestoreProductCommand(Guid Id, Guid? FacilityId = null) : IRequest<Result>;
}
