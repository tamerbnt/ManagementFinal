using Management.Application.Features.Sales.Queries.GetSales;
using Management.Application.Services;
using Management.Application.Features.Sales.Commands.ProcessCheckout;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class SaleService : ISaleService
    {
        private readonly ISender _sender;

        public SaleService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result> ProcessCheckoutAsync(Guid facilityId, CheckoutRequestDto request)
        {
            var result = await _sender.Send(new ProcessCheckoutCommand(facilityId, request));
            return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
        }

        public async Task<Result<decimal>> GetTotalRevenueAsync(Guid facilityId, DateTime start, DateTime end)
        {
            try
            {
                return await _sender.Send(new GetTotalRevenueQuery { FacilityId = facilityId, Start = start, End = end });
            }
            catch (Exception ex)
            {
                return Result.Failure<decimal>(new Error("Sale.RevenueFetchError", ex.Message));
            }
        }

        public async Task<Result<List<SaleDto>>> GetSalesByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await _sender.Send(new GetSalesHistoryQuery { FacilityId = facilityId, Start = start, End = end });
        }

        public async Task<Result<SaleDto>> GetSaleDetailsAsync(Guid facilityId, Guid saleId)
        {
            return await _sender.Send(new GetSaleDetailsQuery(saleId));
        }

        public async Task<Result> CancelSaleAsync(Guid saleId)
        {
            return await _sender.Send(new Management.Application.Features.Sales.Commands.DeleteSale.DeleteSaleCommand(saleId));
        }

        public async Task<Result> CancelSalesByMemberAsync(Guid memberId, Guid facilityId)
        {
            return await _sender.Send(new Management.Application.Features.Sales.Commands.CancelSalesByMember.CancelSalesByMemberCommand(memberId, facilityId));
        }
    }
}
