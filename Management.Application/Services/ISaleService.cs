using System;
using Management.Application.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface ISaleService
    {
        Task<Result> ProcessCheckoutAsync(Guid facilityId, CheckoutRequestDto request);
        Task<Result<decimal>> GetTotalRevenueAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Result<List<SaleDto>>> GetSalesByRangeAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Result<SaleDto>> GetSaleDetailsAsync(Guid facilityId, Guid saleId);
    }
}
