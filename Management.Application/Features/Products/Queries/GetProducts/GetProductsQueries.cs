using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Products.Queries.GetProducts
{
    public record GetActiveProductsQuery(Guid? FacilityId = null) : IRequest<Result<List<ProductDto>>>;
    public record SearchProductsQuery(string SearchTerm, Guid? FacilityId = null) : IRequest<Result<List<ProductDto>>>;
    public record SearchProductsPagedQuery(string SearchTerm, int Page, int PageSize, Guid? FacilityId = null) : IRequest<Result<PagedResult<ProductDto>>>;
    public record GetInventoryStatusQuery(Guid? FacilityId = null) : IRequest<Result<List<ProductDto>>>;
    public record GetProductByIdQuery(Guid Id, Guid? FacilityId = null) : IRequest<Result<ProductDto>>;
}
