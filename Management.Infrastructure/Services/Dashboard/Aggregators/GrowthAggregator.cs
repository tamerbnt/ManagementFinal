using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class GrowthAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;

        public GrowthAggregator(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override int Priority => 35; // Run after primary gym aggregator

        public override bool CanHandle(DashboardContext context) => context.IsGym;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;
            var now = context.UtcNow;
            
            // 1. Calculate Churn Rate (Expired this month / Total Active)
            var startOfMonth = new DateTime(context.LocalToday.Year, context.LocalToday.Month, 1).ToUniversalTime();
            
            var totalActive = await _dbContext.Members
                .CountAsync(m => m.FacilityId == facilityId && m.Status == MemberStatus.Active && !m.IsDeleted);

            var expiredThisMonth = await _dbContext.Members
                .CountAsync(m => m.FacilityId == facilityId && 
                                 m.ExpirationDate >= startOfMonth && 
                                 m.ExpirationDate <= now && 
                                 !m.IsDeleted);

            dto.ChurnRate.Value = totalActive > 0 ? (decimal)expiredThisMonth / totalActive * 100 : 0;
            dto.ChurnRate.PeriodLabel = "MO";
            dto.ChurnRate.IsIncreasePositive = false; // Inverse logic: lower churn is better

            // Comparison: Last Month
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddSeconds(-1);
            
            var expiredLastMonth = await _dbContext.Members
                .CountAsync(m => m.FacilityId == facilityId && 
                                 m.ExpirationDate >= startOfLastMonth && 
                                 m.ExpirationDate <= endOfLastMonth && 
                                 !m.IsDeleted);
            
            dto.ChurnRate.ComparisonValue = totalActive > 0 ? (decimal)expiredLastMonth / totalActive * 100 : 0;
            dto.ChurnRate.PercentChange = CalculatePercentChange(dto.ChurnRate.ComparisonValue, dto.ChurnRate.Value);
            dto.ChurnRate.ComparisonLabel = "vs last month";

            // 2. Calculate Freeze Rate
            var frozenCount = await _dbContext.Members
                .CountAsync(m => m.FacilityId == facilityId && m.Status == MemberStatus.Frozen && !m.IsDeleted);

            var totalPossible = await _dbContext.Members
                .CountAsync(m => m.FacilityId == facilityId && !m.IsDeleted && 
                                 (m.Status == MemberStatus.Active || m.Status == MemberStatus.Frozen || m.Status == MemberStatus.Expired));

            dto.MembershipFreezeRate.Value = totalPossible > 0 ? (decimal)frozenCount / totalPossible * 100 : 0;
            dto.MembershipFreezeRate.PeriodLabel = "LIVE";
            dto.MembershipFreezeRate.ComparisonLabel = "of total base";

            // 3. Growth Trend (Last 6 Months)
            dto.GrowthTrend = await CalculateGrowthTrendAsync(facilityId, context.LocalToday);
        }

        private async Task<List<MemberGrowthDto>> CalculateGrowthTrendAsync(Guid facilityId, DateTime today)
        {
            var trend = new List<MemberGrowthDto>();
            
            for (int i = 5; i >= 0; i--)
            {
                var monthDate = today.AddMonths(-i);
                var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1).ToUniversalTime();
                var monthEnd = monthStart.AddMonths(1).AddSeconds(-1);

                var newMembers = await _dbContext.Members
                    .CountAsync(m => m.FacilityId == facilityId && 
                                     m.CreatedAt >= monthStart && 
                                     m.CreatedAt <= monthEnd && 
                                     !m.IsDeleted);

                var lostMembers = await _dbContext.Members
                    .CountAsync(m => m.FacilityId == facilityId && 
                                     m.ExpirationDate >= monthStart && 
                                     m.ExpirationDate <= monthEnd && 
                                     !m.IsDeleted);

                trend.Add(new MemberGrowthDto
                {
                    Month = monthDate.ToString("MMM"),
                    NewMembers = newMembers,
                    LostMembers = lostMembers
                });
            }

            return trend;
        }
        private decimal CalculatePercentChange(decimal previous, decimal current)
        {
            if (previous == 0) return 0;
            return (current - previous) / previous * 100;
        }
    }
}
