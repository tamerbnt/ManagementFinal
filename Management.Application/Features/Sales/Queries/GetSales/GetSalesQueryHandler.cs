using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Sales.Queries.GetSales
{
    public class GetSalesQueryHandler : 
        IRequestHandler<GetSalesHistoryQuery, Result<List<SaleDto>>>,
        IRequestHandler<GetSaleDetailsQuery, Result<SaleDto>>,
        IRequestHandler<GetTotalRevenueQuery, Result<decimal>>
    {
        private readonly ISaleRepository _saleRepository;

        public GetSalesQueryHandler(ISaleRepository saleRepository)
        {
            _saleRepository = saleRepository;
        }

        public async Task<Result<List<SaleDto>>> Handle(GetSalesHistoryQuery request, CancellationToken cancellationToken)
        {
            var sales = await _saleRepository.GetByDateRangeAsync(request.FacilityId, request.Start, request.End);
            
            // Safety cap for history views - increased for high-volume days
            var limitedSales = sales.Take(2000);

            var dtos = limitedSales.Select(s => new SaleDto
            {
                Id = s.Id,
                Timestamp = s.Timestamp,
                TotalAmount = s.TotalAmount.Amount,
                PaymentMethod = s.PaymentMethod.ToString(),
                TransactionType = s.TransactionType,
                MemberId = s.MemberId,
                ItemsSnapshot = s.Items.ToDictionary(i => i.ProductNameSnapshot, i => i.Quantity)
            }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result<SaleDto>> Handle(GetSaleDetailsQuery request, CancellationToken cancellationToken)
        {
            var sale = await _saleRepository.GetByIdAsync(request.SaleId);
            if (sale == null)
            {
                return Result.Failure<SaleDto>(new Error("Sale.NotFound", $"Sale {request.SaleId} not found."));
            }

            return Result.Success(new SaleDto
            {
                Id = sale.Id,
                Timestamp = sale.Timestamp,
                TotalAmount = sale.TotalAmount.Amount,
                PaymentMethod = sale.PaymentMethod.ToString(),
                TransactionType = sale.TransactionType,
                MemberId = sale.MemberId,
                Items = sale.Items.ToDictionary(i => i.ProductId, i => i.Quantity),
                ItemsSnapshot = sale.Items.ToDictionary(i => i.ProductNameSnapshot, i => i.Quantity)
            });
        }

        public async Task<Result<decimal>> Handle(GetTotalRevenueQuery request, CancellationToken cancellationToken)
        {
            var total = await _saleRepository.GetTotalRevenueAsync(request.FacilityId, request.Start, request.End);
            return Result.Success(total);
        }
    }
}
