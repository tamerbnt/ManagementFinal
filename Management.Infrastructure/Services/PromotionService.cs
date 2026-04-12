using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services;

using Management.Application.Services;
using Management.Application.DTOs;
using Management.Domain.ValueObjects;

public class PromotionService : IPromotionService
{
    private readonly AppDbContext _context;
    private readonly IPricingService _pricingService;
    private readonly ITenantService _tenantService;

    public PromotionService(AppDbContext context, IPricingService pricingService, ITenantService tenantService)
    {
        _context = context;
        _pricingService = pricingService;
        _tenantService = tenantService;
    }

        public async Task<Result<Guid>> CreatePromotionAsync(Guid facilityId, PromotionDto dto)
        {
            try
            {
                var promotion = new Promotion
                {
                    Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
                    TenantId = _tenantService.GetTenantId() ?? Guid.Empty,
                    FacilityId = facilityId,
                    Name = dto.Name,
                    Description = dto.Description,
                    TargetType = dto.TargetType,
                    TargetId = dto.TargetId,
                    DiscountPercentage = dto.IsPercentage ? dto.DiscountValue : null,
                    PromotionPrice = !dto.IsPercentage ? new Money(dto.DiscountValue, "DA") : null,
                    CriteriaGender = dto.RequiredGender,
                    CriteriaMemberPlanId = dto.RequiredMembershipPlanId,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    IsActive = dto.IsActive
                };

                _context.Promotions.Add(promotion);
                await _context.SaveChangesAsync();
                _pricingService.InvalidateCache(facilityId);
                return Result.Success(promotion.Id);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
                return Result.Failure<Guid>(new Error("Promotion.CreateError", message));
            }
        }

        public async Task<Result> UpdatePromotionAsync(Guid facilityId, PromotionDto dto)
        {
            try
            {
                var promotion = await _context.Promotions.FindAsync(dto.Id);
                if (promotion == null) return Result.Failure(new Error("Promotion.NotFound", "Promotion not found."));

                promotion.Name = dto.Name;
                promotion.Description = dto.Description;
                promotion.TargetType = dto.TargetType;
                promotion.TargetId = dto.TargetId;
                promotion.DiscountPercentage = dto.IsPercentage ? dto.DiscountValue : null;
                promotion.PromotionPrice = !dto.IsPercentage ? new Money(dto.DiscountValue, "DA") : null;
                promotion.CriteriaGender = dto.RequiredGender;
                promotion.CriteriaMemberPlanId = dto.RequiredMembershipPlanId;
                promotion.StartDate = dto.StartDate;
                promotion.EndDate = dto.EndDate;
                promotion.IsActive = dto.IsActive;

                await _context.SaveChangesAsync();
                _pricingService.InvalidateCache(facilityId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
                return Result.Failure(new Error("Promotion.UpdateError", message));
            }
        }

        public async Task<Result> DeletePromotionAsync(Guid facilityId, Guid id)
        {
            try
            {
                var promotion = await _context.Promotions.FindAsync(id);
                if (promotion == null) return Result.Failure(new Error("Promotion.NotFound", "Promotion not found."));

                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                _pricingService.InvalidateCache(facilityId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("Promotion.DeleteError", ex.Message));
            }
        }

        public async Task<Result<List<PromotionDto>>> GetPromotionsAsync(Guid facilityId)
        {
            try
            {
                var promos = await _context.Promotions
                    .Where(p => p.FacilityId == facilityId)
                    .AsNoTracking()
                    .ToListAsync();

                var dtos = promos.Select(p => new PromotionDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    TargetType = p.TargetType,
                    TargetId = p.TargetId,
                    DiscountValue = p.DiscountPercentage ?? p.PromotionPrice?.Amount ?? 0,
                    IsPercentage = p.DiscountPercentage.HasValue,
                    RequiredGender = p.CriteriaGender,
                    RequiredMembershipPlanId = p.CriteriaMemberPlanId,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive
                }).ToList();

                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<PromotionDto>>(new Error("Promotion.ListError", ex.Message));
            }
        }

        public async Task<Result<PromotionDto>> GetPromotionAsync(Guid facilityId, Guid id)
        {
            try
            {
                var p = await _context.Promotions.FindAsync(id);
                if (p == null) return Result.Failure<PromotionDto>(new Error("Promotion.NotFound", "Promotion not found."));

                var dto = new PromotionDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    TargetType = p.TargetType,
                    TargetId = p.TargetId,
                    DiscountValue = p.DiscountPercentage ?? p.PromotionPrice?.Amount ?? 0,
                    IsPercentage = p.DiscountPercentage.HasValue,
                    RequiredGender = p.CriteriaGender,
                    RequiredMembershipPlanId = p.CriteriaMemberPlanId,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive
                };

                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                return Result.Failure<PromotionDto>(new Error("Promotion.GetError", ex.Message));
            }
        }
    }
