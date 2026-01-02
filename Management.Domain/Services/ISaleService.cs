using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Primitives;

namespace Management.Domain.Services
{
    public interface ISaleService
    {
        Task<Result> ProcessCheckoutAsync(Guid facilityId, CheckoutRequestDto request);
        Task<Result<List<SaleDto>>> GetSalesByRangeAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Result<SaleDto>> GetSaleDetailsAsync(Guid facilityId, Guid saleId);
    }
}