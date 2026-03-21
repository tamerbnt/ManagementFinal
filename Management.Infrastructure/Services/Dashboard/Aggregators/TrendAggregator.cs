using LiveChartsCore.Defaults;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
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
    public class TrendAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;
        private readonly IAccessEventRepository _accessEventRepository;
        private readonly IReservationRepository _reservationRepository;

        public TrendAggregator(
            AppDbContext dbContext,
            IAccessEventRepository accessEventRepository,
            IReservationRepository reservationRepository)
        {
            _dbContext = dbContext;
            _accessEventRepository = accessEventRepository;
            _reservationRepository = reservationRepository;
        }

        public override int Priority => 40;

        public override bool CanHandle(DashboardContext context) => true;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;

            // 1. Revenue Trend (30 days)
            dto.RevenueTrend = await GenerateRevenueTrendAsync(context);

            // 2. Occupancy/Member Trend (Facility Specific)
            dto.MemberTrend = context.FacilityType switch
            {
                Management.Domain.Enums.FacilityType.Restaurant => await GetRestaurantOccupancyTrendAsync(context),
                Management.Domain.Enums.FacilityType.Salon => await GetSalonOccupancyTrendAsync(context),
                Management.Domain.Enums.FacilityType.Gym => await GetGymOccupancyTrendAsync(context),
                _ => new List<DateTimePoint>()
            };
        }

        private async Task<List<DateTimePoint>> GenerateRevenueTrendAsync(DashboardContext context)
        {
            var trend = new List<DateTimePoint>();
            var today = context.LocalToday;
            var thirtyDaysAgo = today.AddDays(-29);
            var utcStartThreshold = thirtyDaysAgo.ToUniversalTime();

            var salesData = await _dbContext.Sales
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.FacilityId == context.FacilityId && (s.TenantId == context.TenantId || s.TenantId == Guid.Empty) && s.Timestamp >= utcStartThreshold)
                .GroupBy(s => s.Timestamp.ToLocalTime().Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(s => (double)s.TotalAmount.Amount) })
                .ToListAsync();
            
            var salesMap = salesData.ToDictionary(x => x.Date, x => x.Total);

            var restaurantMap = new Dictionary<DateTime, double>();
            if (context.IsRestaurant)
            {
                 var restaurantData = await _dbContext.RestaurantOrders
                    .AsNoTracking()
                    .Where(o => o.FacilityId == context.FacilityId && o.CompletedAt >= utcStartThreshold &&
                                (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                    .GroupBy(o => o.CompletedAt!.Value.ToLocalTime().Date)
                    .Select(g => new { Date = g.Key, Total = g.Sum(o => (double)(o.Subtotal + o.Tax)) })
                    .ToListAsync();
                restaurantMap = restaurantData.ToDictionary(x => x.Date, x => x.Total);
            }

            for (int i = 29; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                double total = 0;
                if (salesMap.TryGetValue(date, out var saleTotal)) total += saleTotal;
                if (restaurantMap.TryGetValue(date, out var restTotal)) total += restTotal;
                trend.Add(new DateTimePoint(date, total));
            }

            return trend;
        }

        private async Task<List<DateTimePoint>> GetGymOccupancyTrendAsync(DashboardContext context)
        {
            var trend = new List<DateTimePoint>();
            var events = await _accessEventRepository.GetByDateRangeAsync(context.FacilityId, context.UtcDayStart, context.UtcDayEnd);
            var granted = events.Where(e => e.IsAccessGranted).ToList();

            for (int h = 0; h <= 23; h++)
            {
                var hourSlot = context.LocalToday.AddHours(h);
                var utcHourSlot = hourSlot.ToUniversalTime();
                var utcHourEnd = utcHourSlot.AddHours(1);
                int count = granted.Count(e => e.Timestamp >= utcHourSlot && e.Timestamp < utcHourEnd);
                trend.Add(new DateTimePoint(hourSlot, count));
            }
            return trend;
        }

        private async Task<List<DateTimePoint>> GetSalonOccupancyTrendAsync(DashboardContext context)
        {
            var trend = new List<DateTimePoint>();
            var reservations = await _reservationRepository.GetByDateRangeAsync(context.UtcDayStart, context.UtcDayEnd, context.FacilityId);

            for (int i = 8; i <= 22; i++)
            {
                var timeSlot = context.LocalToday.AddHours(i);
                var utcTimeSlot = timeSlot.ToUniversalTime();
                int count = reservations.Count(a => a.StartTime <= utcTimeSlot && a.EndTime > utcTimeSlot && a.Status != "Cancelled");
                trend.Add(new DateTimePoint(timeSlot, count));
            }
            return trend;
        }

        private async Task<List<DateTimePoint>> GetRestaurantOccupancyTrendAsync(DashboardContext context)
        {
            var trend = new List<DateTimePoint>();
            var orders = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .Where(o => o.FacilityId == context.FacilityId && o.CreatedAt >= context.UtcDayStart && o.CreatedAt < context.UtcDayEnd)
                .ToListAsync();

            for (int h = 0; h <= 23; h++)
            {
                var hourSlot = context.LocalToday.AddHours(h);
                var utcHourSlot = hourSlot.ToUniversalTime();
                var utcHourEnd = utcHourSlot.AddHours(1);
                int count = orders.Where(o => o.CreatedAt >= utcHourSlot && o.CreatedAt < utcHourEnd).Sum(o => o.PartySize);
                trend.Add(new DateTimePoint(hourSlot, count));
            }
            return trend;
        }
    }
}
