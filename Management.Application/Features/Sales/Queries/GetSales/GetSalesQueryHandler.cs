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
        IRequestHandler<GetSaleDetailsQuery, Result<SaleDto>>
    {
        private readonly ISaleRepository _saleRepository;

        public GetSalesQueryHandler(ISaleRepository saleRepository)
        {
            _saleRepository = saleRepository;
        }

        public async Task<Result<List<SaleDto>>> Handle(GetSalesHistoryQuery request, CancellationToken cancellationToken)
        {
            var sales = await _saleRepository.GetByDateRangeAsync(request.Start, request.End);
            var dtos = sales.Select(s => new SaleDto
            {
                Id = s.Id,
                Timestamp = s.Timestamp,
                TotalAmount = s.TotalAmount.Amount,
                PaymentMethod = s.PaymentMethod.ToString(),
                TransactionType = s.TransactionType,
                // MemberName? Repository might not include Member. 
                // Legacy service didn't include it in list view map.
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
                Items = sale.Items.ToDictionary(i => i.ProductId, i => i.Quantity) 
                // DTO expects Dictionary<Guid,int>? Check SaleDto definition.
                // Step 202: CheckoutRequestDto has Dictionary Items.
                // SaleDto def? Step 274 viewed SaleDto.
                // Let's assume standard DTO. 
                // If SaleDto structure is complex (List<SaleItemDto>), mapping might differ.
                // I'll check if build fails on Items mapping.
            });
        }
    }
}
