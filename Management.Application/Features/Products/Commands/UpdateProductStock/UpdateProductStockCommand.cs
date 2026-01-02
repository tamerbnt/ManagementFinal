using System;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Products.Commands.UpdateProductStock
{
    public record UpdateProductStockCommand(Guid ProductId, int QuantityChange, string Reason) : IRequest<Result>;
}
