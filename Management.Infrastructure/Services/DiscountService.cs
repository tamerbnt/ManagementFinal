using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services
{
    public class DiscountService : IDiscountService
    {
        private readonly AppDbContext _context;
        private readonly ITenantService _tenantService;

        public DiscountService(AppDbContext context, ITenantService tenantService)
        {
            _context = context;
            _tenantService = tenantService;
        }

        public async Task<Result<Guid>> CreateDiscountAsync(Guid facilityId, DiscountDto dto)
        {
            try
            {
                var discount = new Discount
                {
                    Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
                    TenantId = _tenantService.GetTenantId() ?? Guid.Empty,
                    FacilityId = facilityId,
                    Name = dto.Name,
                    Description = dto.Description,
                    Value = dto.Value,
                    IsPercentage = dto.IsPercentage,
                    IsActive = dto.IsActive
                };

                _context.Discounts.Add(discount);
                await _context.SaveChangesAsync();
                return Result.Success(discount.Id);
            }
            catch (Exception ex)
            {
                return Result.Failure<Guid>(new Error("Discount.CreateError", ex.Message));
            }
        }

        public async Task<Result> UpdateDiscountAsync(Guid facilityId, DiscountDto dto)
        {
            try
            {
                var discount = await _context.Discounts.FindAsync(dto.Id);
                if (discount == null) return Result.Failure(new Error("Discount.NotFound", "Discount not found."));

                discount.UpdateDetails(dto.Name, dto.Description, dto.Value, dto.IsPercentage, dto.IsActive);

                await _context.SaveChangesAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("Discount.UpdateError", ex.Message));
            }
        }

        public async Task<Result> DeleteDiscountAsync(Guid facilityId, Guid id)
        {
            try
            {
                var discount = await _context.Discounts.FindAsync(id);
                if (discount == null) return Result.Failure(new Error("Discount.NotFound", "Discount not found."));

                _context.Discounts.Remove(discount);
                await _context.SaveChangesAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("Discount.DeleteError", ex.Message));
            }
        }

        public async Task<Result<List<DiscountDto>>> GetDiscountsAsync(Guid facilityId)
        {
            try
            {
                var discounts = await _context.Discounts
                    .Where(d => d.FacilityId == facilityId)
                    .AsNoTracking()
                    .Select(d => new DiscountDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Description = d.Description,
                        Value = d.Value,
                        IsPercentage = d.IsPercentage,
                        IsActive = d.IsActive
                    })
                    .ToListAsync();

                return Result.Success(discounts);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<DiscountDto>>(new Error("Discount.ListError", ex.Message));
            }
        }

        public async Task<Result<List<DiscountDto>>> GetActiveDiscountsAsync(Guid facilityId)
        {
            try
            {
                var discounts = await _context.Discounts
                    .Where(d => d.FacilityId == facilityId && d.IsActive)
                    .AsNoTracking()
                    .Select(d => new DiscountDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Description = d.Description,
                        Value = d.Value,
                        IsPercentage = d.IsPercentage,
                        IsActive = d.IsActive
                    })
                    .ToListAsync();

                return Result.Success(discounts);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<DiscountDto>>(new Error("Discount.ActiveListError", ex.Message));
            }
        }

        public async Task<Result<DiscountDto>> GetDiscountAsync(Guid facilityId, Guid id)
        {
            try
            {
                var d = await _context.Discounts.FindAsync(id);
                if (d == null) return Result.Failure<DiscountDto>(new Error("Discount.NotFound", "Discount not found."));

                var dto = new DiscountDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    Value = d.Value,
                    IsPercentage = d.IsPercentage,
                    IsActive = d.IsActive
                };

                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                return Result.Failure<DiscountDto>(new Error("Discount.GetError", ex.Message));
            }
        }
    }
}
