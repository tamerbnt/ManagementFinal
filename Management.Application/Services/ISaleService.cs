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
        Task<Result<Guid>> ProcessCheckoutAsync(Guid facilityId, CheckoutRequestDto request, bool publishNotification = true);
        Task<Result<decimal>> GetTotalRevenueAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Result<List<SaleDto>>> GetSalesByRangeAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Result<SaleDto>> GetSaleDetailsAsync(Guid facilityId, Guid saleId);
        Task<Result> CancelSaleAsync(Guid saleId);
        Task<Result> CancelSalesByMemberAsync(Guid memberId, Guid facilityId);
    }
}
