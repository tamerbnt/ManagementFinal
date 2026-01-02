using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Products.Queries.GetProducts
{
    public record GetActiveProductsQuery() : IRequest<Result<List<ProductDto>>>;
    public record SearchProductsQuery(string SearchTerm) : IRequest<Result<List<ProductDto>>>;
    public record GetInventoryStatusQuery() : IRequest<Result<List<ProductDto>>>;
    public record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;
}
