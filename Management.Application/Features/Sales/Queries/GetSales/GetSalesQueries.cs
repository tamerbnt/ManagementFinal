using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Sales.Queries.GetSales
{
    public record GetSalesHistoryQuery(DateTime Start, DateTime End) : IRequest<Result<List<SaleDto>>>;
    public record GetSaleDetailsQuery(Guid SaleId) : IRequest<Result<SaleDto>>;
}
