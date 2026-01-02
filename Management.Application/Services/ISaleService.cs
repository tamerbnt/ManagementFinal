using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

namespace Management.Application.Services
{
    public interface ISaleService
    {
        Task<Result> ProcessCheckoutAsync(Guid facilityId, CheckoutRequestDto request);
        Task<Result<List<SaleDto>>> GetSalesByRangeAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Result<SaleDto>> GetSaleDetailsAsync(Guid facilityId, Guid saleId);
    }
}