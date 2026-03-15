using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Models.Restaurant;
using Management.Infrastructure.Data;
using OrderStatus = Management.Domain.Models.Restaurant.OrderStatus;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class RestaurantAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;

        public RestaurantAggregator(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override int Priority => 20;

        public override bool CanHandle(DashboardContext context) => context.IsRestaurant;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;

            // 1. Tables
            var tables = await _dbContext.RestaurantTables
                .AsNoTracking()
                .Where(t => t.FacilityId == facilityId)
                .ToListAsync();
            
            dto.TotalTablesCount = tables.Count;
            dto.ActiveTablesCount = tables.Count(t => t.Status == TableStatus.Occupied || 
                                                     t.Status == TableStatus.OrderSent || 
                                                     t.Status == TableStatus.BillRequested || 
                                                     t.Status == TableStatus.Ready);
            dto.ActiveMembers = dto.ActiveTablesCount; // Map to primary stat

            // 2. Kitchen Stats
            dto.PendingOrdersCount = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .Where(o => o.FacilityId == facilityId && 
                            (o.Status == OrderStatus.Pending || o.Status == OrderStatus.InProgress || o.Status == OrderStatus.InKitchen || o.Status == OrderStatus.Ready || o.Status == OrderStatus.Delivered))
                .CountAsync();

            var totalOrdersToday = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .Where(o => o.FacilityId == facilityId && 
                            o.CreatedAt >= context.UtcDayStart && o.CreatedAt < context.UtcDayEnd)
                .CountAsync();
            dto.CheckInsToday = totalOrdersToday; // Map to hero card

            // 3. Covers and AOV
            // FIX: Covers should include all orders started today (CreatedAt)
            var allStartedToday = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .Where(o => o.FacilityId == facilityId && 
                            o.CreatedAt >= context.UtcDayStart && o.CreatedAt < context.UtcDayEnd &&
                            o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            // FIX: AOV should include all orders completed today (CompletedAt), regardless of when they started
            var completedToday = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .Where(o => o.FacilityId == facilityId && 
                            o.CompletedAt >= context.UtcDayStart && o.CompletedAt < context.UtcDayEnd &&
                            (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                .ToListAsync();
            
            dto.TodayCovers = allStartedToday.Sum(o => o.PartySize);
            dto.AverageOrderValue = completedToday.Any() ? completedToday.Average(o => o.Total) : 0;

            // 4. Popular Items
            dto.PopularItems = await _dbContext.OrderItems
                .AsNoTracking()
                .Where(oi => _dbContext.RestaurantOrders
                    .Any(ro => ro.Id == oi.RestaurantOrderId && 
                               ro.FacilityId == facilityId && 
                               ro.CompletedAt >= context.UtcDayStart && ro.CompletedAt < context.UtcDayEnd))
                .GroupBy(oi => oi.Name)
                })
                .OrderByDescending(x => x.Quantity)
                .Take(5)
                .ToListAsync();


            
            var totalQuantity = dto.PopularItems.Sum(x => x.Quantity);
            foreach (var item in dto.PopularItems)
            {
                item.Percentage = totalQuantity > 0 ? (double)item.Quantity / totalQuantity * 100 : 0;
            }
        }
    }
}
