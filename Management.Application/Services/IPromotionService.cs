using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IPromotionService
    {
        Task<Result<Guid>> CreatePromotionAsync(Guid facilityId, PromotionDto promotion);
        Task<Result> UpdatePromotionAsync(Guid facilityId, PromotionDto promotion);
        Task<Result> DeletePromotionAsync(Guid facilityId, Guid id);
        Task<Result<List<PromotionDto>>> GetPromotionsAsync(Guid facilityId);
        Task<Result<PromotionDto>> GetPromotionAsync(Guid facilityId, Guid id);
    }
}
