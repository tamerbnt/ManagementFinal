using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Application.Interfaces.App
{
    public interface IDiscountService
    {
        Task<Result<Guid>> CreateDiscountAsync(Guid facilityId, DiscountDto dto);
        Task<Result> UpdateDiscountAsync(Guid facilityId, DiscountDto dto);
        Task<Result> DeleteDiscountAsync(Guid facilityId, Guid id);
        Task<Result<List<DiscountDto>>> GetDiscountsAsync(Guid facilityId);
        Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync(Guid facilityId);
        Task<Result<DiscountDto>> GetDiscountAsync(Guid facilityId, Guid id);
    }
}
