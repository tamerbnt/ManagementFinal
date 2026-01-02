using Management.Application.Features.Sales.Queries.GetSales;
using Management.Application.Services;
using Management.Application.Features.Sales.Commands.ProcessCheckout;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

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
            var result = await _sender.Send(new ProcessCheckoutCommand(request));
            return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
        }

        public async Task<Result<List<SaleDto>>> GetSalesByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await _sender.Send(new GetSalesHistoryQuery(start, end));
        }

        public async Task<Result<SaleDto>> GetSaleDetailsAsync(Guid facilityId, Guid saleId)
        {
            return await _sender.Send(new GetSaleDetailsQuery(saleId));
        }
    }
}
