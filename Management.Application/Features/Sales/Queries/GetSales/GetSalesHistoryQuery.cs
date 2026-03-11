using MediatR;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using System.Collections.Generic;
using System;

namespace Management.Application.Features.Sales.Queries.GetSales
{
    public class GetSalesHistoryQuery : IRequest<Result<List<SaleDto>>>
    {
        public Guid FacilityId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
