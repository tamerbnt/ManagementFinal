using System;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Sales.Queries.GetSales
{
    public class GetTotalRevenueQuery : IRequest<Result<decimal>>
    {
        public Guid FacilityId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
