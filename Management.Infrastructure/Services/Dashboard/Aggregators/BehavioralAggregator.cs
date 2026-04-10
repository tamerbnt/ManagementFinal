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
    public class BehavioralAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;

        public BehavioralAggregator(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override int Priority => 38;

        public override bool CanHandle(DashboardContext context) => context.IsGym;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;
            var startOfLastWeek = context.UtcNow.AddDays(-7);

            // 1. Visit Frequency Distribution (Last 7 Days)
            var checkinsPerMember = await _dbContext.AccessEvents
                .Where(ae => ae.FacilityId == facilityId && ae.Timestamp >= startOfLastWeek && ae.IsAccessGranted && !string.IsNullOrEmpty(ae.CardId))
                .Join(_dbContext.Members, ae => ae.CardId, m => m.CardId, (ae, m) => new { m.Id, ae.Timestamp })
                .GroupBy(x => x.Id)
                .Select(g => g.Count())
                .ToListAsync();

            var distribution = new int[6]; // 0, 1, 2, 3, 4, 5+
            foreach (var count in checkinsPerMember)
            {
                int bucket = Math.Min(count, 5);
                distribution[bucket]++;
            }
            
            // For those with 0 visits, we need active members minus those who visited
            var activeMemberCount = await _dbContext.Members.CountAsync(m => m.FacilityId == facilityId && m.Status == MemberStatus.Active && !m.IsDeleted);
            distribution[0] = Math.Max(0, activeMemberCount - checkinsPerMember.Count);
            
            dto.VisitFrequencyDistribution = distribution.ToList();
            dto.AvgVisitsPerWeek.Value = checkinsPerMember.Any() ? (decimal)checkinsPerMember.Average() : 0;
            dto.AvgVisitsPerWeek.PeriodLabel = "7D";
            dto.AvgVisitsPerWeek.ComparisonLabel = "avg/week";

            // 2. Metrics (PT Upsell is owned by GymAggregator; Trial Conversion moved here)
            var thirtyDaysAgo = context.UtcNow.AddDays(-30);
            var priorStart = thirtyDaysAgo.AddDays(-30);

            // Denominator: All walk-in leads registered in the last 30 days
            var totalWalkInLeads = await _dbContext.Members
                .CountAsync(m => m.FacilityId == facilityId
                              && m.Source == "Walk-in"
                              && m.CreatedAt >= thirtyDaysAgo
                              && !m.IsDeleted);

            // Numerator: Walk-in leads who graduated to Active with a real membership plan
            var convertedLeads = await _dbContext.Members
                .CountAsync(m => m.FacilityId == facilityId
                              && m.Source == "Walk-in"
                              && m.Status == MemberStatus.Active
                              && m.MembershipPlanId != null
                              && m.CreatedAt >= thirtyDaysAgo
                              && !m.IsDeleted);

            if (totalWalkInLeads > 0)
            {
                dto.TrialConversionRate.Value = (decimal)convertedLeads / totalWalkInLeads * 100;
                dto.TrialConversionRate.PeriodLabel = "MO";
                dto.TrialConversionRate.ComparisonLabel = "of walk-ins converted";
                dto.TrialConversionRate.IsIncreasePositive = true;

                // PercentChange delta calculation
                var priorTotal = await _dbContext.Members
                    .CountAsync(m => m.FacilityId == facilityId
                                  && m.Source == "Walk-in"
                                  && m.CreatedAt >= priorStart && m.CreatedAt < thirtyDaysAgo
                                  && !m.IsDeleted);
                var priorConverted = await _dbContext.Members
                    .CountAsync(m => m.FacilityId == facilityId
                                  && m.Source == "Walk-in"
                                  && m.Status == MemberStatus.Active
                                  && m.MembershipPlanId != null
                                  && m.CreatedAt >= priorStart && m.CreatedAt < thirtyDaysAgo
                                  && !m.IsDeleted);

                if (priorTotal > 0)
                {
                    var priorRate = (decimal)priorConverted / priorTotal * 100;
                    dto.TrialConversionRate.ComparisonValue = priorRate;
                    dto.TrialConversionRate.PercentChange = priorRate > 0
                        ? (int)((dto.TrialConversionRate.Value - priorRate) / priorRate * 100)
                        : 0;
                }
            }
        }
    }
}
