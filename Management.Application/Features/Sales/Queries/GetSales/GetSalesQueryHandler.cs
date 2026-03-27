using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
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
        private readonly IMemberRepository _memberRepository;

        public GetSalesQueryHandler(ISaleRepository saleRepository, IMemberRepository memberRepository)
        {
            _saleRepository = saleRepository;
            _memberRepository = memberRepository;
        }

        public async Task<Result<List<SaleDto>>> Handle(GetSalesHistoryQuery request, CancellationToken cancellationToken)
        {
            var sales = await _saleRepository.GetByDateRangeAsync(request.FacilityId, request.Start, request.End);
            
            // Resolve member names and filter out sales from deleted members
            var memberIds = sales.Where(s => s.MemberId.HasValue).Select(s => s.MemberId!.Value).Distinct().ToList();
            var members = new Dictionary<Guid, Member>();
            
            if (memberIds.Any())
            {
                // We use search or list; but to ensure we respect deletion, we just get them normally.
                // Standard repository GetByIdAsync/GetAllAsync filters out IsDeleted by default.
                foreach (var id in memberIds)
                {
                    var m = await _memberRepository.GetByIdAsync(id);
                    if (m != null) members[id] = m;
                }
            }

            var dtos = sales
                .Where(s => !s.MemberId.HasValue || members.ContainsKey(s.MemberId.Value)) // Exclude if member exists then was deleted
                .Take(2000)
                .Select(s => new SaleDto
                {
                    Id = s.Id,
                    Timestamp = s.Timestamp,
                    TotalAmount = s.TotalAmount.Amount,
                    PaymentMethod = s.PaymentMethod.ToString(),
                    TransactionType = s.TransactionType,
                    MemberId = s.MemberId,
                    MemberName = s.MemberId.HasValue && members.TryGetValue(s.MemberId.Value, out var m) ? m.FullName : "Guest",
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
