using MediatR;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using System;

namespace Management.Application.Features.Sales.Queries.GetSales
{
    public class GetSaleDetailsQuery : IRequest<Result<SaleDto>>
    {
        public Guid SaleId { get; set; }
        public GetSaleDetailsQuery(Guid saleId) => SaleId = saleId;
    }
}
