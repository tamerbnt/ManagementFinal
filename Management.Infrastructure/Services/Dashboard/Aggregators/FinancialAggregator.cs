using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Management.Infrastructure.Data;
using OrderStatus = Management.Domain.Models.Restaurant.OrderStatus;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class FinancialAggregator : BaseAggregator
    {
        private readonly ISaleRepository _saleRepository;
        private readonly AppDbContext _dbContext;

        public FinancialAggregator(ISaleRepository saleRepository, AppDbContext dbContext)
        {
            _saleRepository = saleRepository;
            _dbContext = dbContext;
        }

        public override int Priority => 10; // First - core data

        public override bool CanHandle(DashboardContext context) => true; // All facilities have finance

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;


            // 1. Revenue Aggregation
            decimal yesterdayRevenue = 0;
            if (context.IsGym || context.IsSalon)
            {
                dto.MonthlyRevenue = await _saleRepository.GetTotalRevenueAsync(facilityId, context.UtcMonthStart, context.UtcNow);
                dto.DailyRevenue = await _saleRepository.GetTotalRevenueAsync(facilityId, context.UtcDayStart, context.UtcDayEnd);
                yesterdayRevenue = await _saleRepository.GetTotalRevenueAsync(facilityId, context.UtcYesterdayStart, context.UtcYesterdayEnd);
            }


            if (context.IsRestaurant)
            {
                var monthlyRevenueSum = await _dbContext.RestaurantOrders
                    .AsNoTracking()
                    .Where(o => o.FacilityId == facilityId && 
                                o.CompletedAt >= context.UtcMonthStart && o.CompletedAt < context.UtcNow &&
                                (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                    .Select(o => (double)(o.Subtotal + o.Tax))
                    .SumAsync();
                dto.MonthlyRevenue += (decimal)monthlyRevenueSum;

                var dailyRevenueSum = await _dbContext.RestaurantOrders
                    .AsNoTracking()
                    .Where(o => o.FacilityId == facilityId && 
                                o.CompletedAt >= context.UtcDayStart && o.CompletedAt < context.UtcDayEnd &&
                                (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                    .Select(o => (double)(o.Subtotal + o.Tax))
                    .SumAsync();
                dto.DailyRevenue += (decimal)dailyRevenueSum;

                var yesterdayRevenueSum = await _dbContext.RestaurantOrders
                    .AsNoTracking()
                    .Where(o => o.FacilityId == facilityId && 
                                o.CompletedAt >= context.UtcYesterdayStart && o.CompletedAt < context.UtcYesterdayEnd &&
                                (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                    .Select(o => (double)(o.Subtotal + o.Tax))
                    .SumAsync();
                yesterdayRevenue += (decimal)yesterdayRevenueSum;
            }

            // 2. Expenses
            dto.DailyExpenses = await GetTotalExpensesInRangeAsync(facilityId, context.UtcDayStart, context.UtcDayEnd, context);

            dto.MonthlyExpenses = await GetTotalExpensesInRangeAsync(facilityId, context.UtcMonthStart, context.UtcNow, context);
            var yesterdayExpenses = await GetTotalExpensesInRangeAsync(facilityId, context.UtcYesterdayStart, context.UtcYesterdayEnd, context);

            // 3. Profit & Trends
            dto.NetProfit = dto.DailyRevenue - dto.DailyExpenses;

            var yesterdayProfit = yesterdayRevenue - yesterdayExpenses;

            dto.RevenuePercentChange = CalculatePercentageChange(yesterdayRevenue, dto.DailyRevenue);
            dto.ExpensesPercentChange = CalculatePercentageChange(yesterdayExpenses, dto.DailyExpenses);
            dto.NetProfitPercentChange = CalculatePercentageChange(yesterdayProfit, dto.NetProfit);

            // 4. Target
            var settings = await _dbContext.GymSettings
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.FacilityId == facilityId && (s.TenantId == context.TenantId || s.TenantId == Guid.Empty));
            dto.DailyRevenueTarget = settings?.DailyRevenueTarget ?? 10_000m;
        }

        private async Task<decimal> GetTotalExpensesInRangeAsync(Guid facilityId, DateTime start, DateTime end, DashboardContext context)
        {
            // COGS
            var salesIds = await _dbContext.Sales
                .IgnoreQueryFilters()
                .Where(s => s.FacilityId == facilityId && (s.TenantId == context.TenantId || s.TenantId == Guid.Empty) && s.Timestamp >= start && s.Timestamp < end && !s.IsDeleted)
                .Select(s => s.Id)
                .ToListAsync();

            var cogsSumDouble = await _dbContext.SaleItems
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(si => salesIds.Contains(si.SaleId) && (si.TenantId == context.TenantId || si.TenantId == Guid.Empty))
                .Join(_dbContext.Products.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId && (p.TenantId == context.TenantId || p.TenantId == Guid.Empty)), 
                      si => si.ProductId, 
                      p => p.Id, 
                      (si, p) => si.Quantity * p.Cost.Amount)
                .Select(x => (double)x)
                .SumAsync();

            var cogsCount = await _dbContext.SaleItems
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(si => salesIds.Contains(si.SaleId) && (si.TenantId == context.TenantId || si.TenantId == Guid.Empty))
                .Join(_dbContext.Products.IgnoreQueryFilters().Where(p => p.FacilityId == facilityId && (p.TenantId == context.TenantId || p.TenantId == Guid.Empty)), 
                      si => si.ProductId, 
                      p => p.Id, 
                      (si, p) => si.Quantity * p.Cost.Amount)
                .CountAsync();

            var cogs = (decimal)cogsSumDouble;


            // Payroll
            var payrollSumDouble = await _dbContext.PayrollEntries
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.FacilityId == facilityId && (p.TenantId == context.TenantId || p.TenantId == Guid.Empty) && (p.UpdatedAt ?? p.CreatedAt) >= start && (p.UpdatedAt ?? p.CreatedAt) < end)
                .Select(p => (double)p.PaidAmount.Amount)
                .SumAsync();

            var payrollCount = await _dbContext.PayrollEntries
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.FacilityId == facilityId && (p.TenantId == context.TenantId || p.TenantId == Guid.Empty) && (p.UpdatedAt ?? p.CreatedAt) >= start && (p.UpdatedAt ?? p.CreatedAt) < end)
                .CountAsync();

            var payroll = (decimal)payrollSumDouble;


            // inventory_purchases.date stores local dates, so convert UTC bounds to local before comparing
            var startStr = start.ToLocalTime().ToString("yyyy-MM-dd");
            var endStr = end.ToLocalTime().ToString("yyyy-MM-dd");
            var facilityIdStr = facilityId.ToString().ToLower();

            var inventory = await _dbContext.Database.SqlQueryRaw<decimal>(
                "SELECT COALESCE(SUM(total_price), 0) AS Value FROM inventory_purchases WHERE facility_id = {0} AND date >= {1} AND date < {2}",
                facilityIdStr, startStr, endStr
            ).FirstOrDefaultAsync();


            return cogs + payroll + inventory;
        }
    }
}
