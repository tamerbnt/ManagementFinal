using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    /// <summary>
    /// Aggregator responsible for identifying at-risk members based on attendance patterns.
    /// Implements the "Retention Guardian" logic.
    /// </summary>
    public class RetentionAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;

        public RetentionAggregator(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override int Priority => 30; // Run after primary Gym/Salon aggregators

        public override bool CanHandle(DashboardContext context) => true; // Applicable for both Gym and Salon

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;
            var thresholdDate = context.UtcNow.AddDays(-14);
            var newMemberThreshold = context.UtcNow.AddDays(-30);

            // 1. Fetch Active members who aren't "New" (joined > 30 days ago) 
            var activeMembers = await _dbContext.Members
                .Where(m => m.FacilityId == facilityId &&
                            m.Status == MemberStatus.Active &&
                            m.CreatedAt < newMemberThreshold &&
                            !m.IsDeleted)
                .Select(m => new
                {
                    m.Id,
                    m.FullName,
                    m.Email,
                    m.Status,
                    m.CardId
                })
                .ToListAsync();

            if (!activeMembers.Any()) return;

            // Map valid CardIds to their Member info
            var cardToMemberMap = activeMembers
                .Where(m => !string.IsNullOrEmpty(m.CardId))
                .ToDictionary(m => m.CardId, m => m);

            var cardIds = cardToMemberMap.Keys.ToList();

            // 2. Find the last successful check-in for these cards
            var lastVisits = await _dbContext.AccessEvents
                .Where(ae => ae.FacilityId == facilityId &&
                             ae.IsAccessGranted &&
                             cardIds.Contains(ae.CardId))
                .GroupBy(ae => ae.CardId)
                .Select(g => new
                {
                    CardId = g.Key,
                    LastVisit = g.Max(ae => ae.Timestamp)
                })
                .ToListAsync();

            var visitLookup = lastVisits.ToDictionary(v => v.CardId, v => v.LastVisit);

            // 3. Process Risks
            foreach (var memberInfo in activeMembers)
            {
                DateTime? lastVisit = null;
                if (!string.IsNullOrEmpty(memberInfo.CardId) && visitLookup.TryGetValue(memberInfo.CardId, out var date))
                {
                    lastVisit = date;
                }

                double daysSinceLastVisit;
                if (lastVisit.HasValue)
                {
                    daysSinceLastVisit = (context.UtcNow - lastVisit.Value).TotalDays;
                }
                else
                {
                    // Never visited but joined > 30 days ago? High risk.
                    daysSinceLastVisit = 999; 
                }

                if (daysSinceLastVisit >= 14)
                {
                    var risk = new ChurnRiskDto
                    {
                        MemberName = memberInfo.FullName,
                        Email = memberInfo.Email?.ToString() ?? string.Empty,
                        Status = memberInfo.Status.ToString(),
                        DaysSinceLastVisit = (int)Math.Min(daysSinceLastVisit, 999),
                        RiskLevel = daysSinceLastVisit > 30 ? "High" : "Medium",
                        Reason = lastVisit.HasValue 
                            ? $"No visit in { (int)daysSinceLastVisit } days" 
                            : "No recorded visits since joining"
                    };

                    dto.ChurnRisks.Add(risk);
                }
            }

            // Sort by risk (High first) then by days away
            var sortedRisks = dto.ChurnRisks
                .OrderByDescending(r => r.RiskLevel == "High")
                .ThenByDescending(r => r.DaysSinceLastVisit)
                .Take(10) // Only top 10 for dashboard performance
                .ToList();

            dto.ChurnRisks.Clear();
            foreach (var r in sortedRisks) dto.ChurnRisks.Add(r);
        }
    }
}
