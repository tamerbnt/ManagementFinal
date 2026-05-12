using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class GymAggregator : BaseAggregator
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IAccessEventRepository _accessEventRepository;
        private readonly IRegistrationRepository _registrationRepository;
        private readonly AppDbContext _dbContext;

        public GymAggregator(
            IMemberRepository memberRepository,
            IAccessEventRepository accessEventRepository,
            IRegistrationRepository registrationRepository,
            AppDbContext dbContext)
        {
            _memberRepository = memberRepository;
            _accessEventRepository = accessEventRepository;
            _registrationRepository = registrationRepository;
            _dbContext = dbContext;
        }

        public override int Priority => 20;

        public override bool CanHandle(DashboardContext context) => context.IsGym;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;

            dto.TotalMembers = await _memberRepository.GetTotalCountAsync(facilityId);
            dto.ActiveMembers = await _memberRepository.GetActiveCountAsync(facilityId);
            dto.PendingRegistrationsCount = await _registrationRepository.GetCountByStatusAsync(Management.Domain.Enums.RegistrationStatus.Pending, facilityId);

            // Active members as of yesterday (members whose plan hadn't expired by yesterday's start)
            dto.ActiveMembersYesterday = await _dbContext.Members
                .AsNoTracking()
                .Where(m => m.FacilityId == facilityId
                    && m.Status == Management.Domain.Enums.MemberStatus.Active
                    && !m.IsDeleted
                    && m.ExpirationDate > context.UtcYesterdayStart)
                .CountAsync();
            dto.ExpiringSoonCount = await _memberRepository.GetExpiringCountAsync(context.LocalToday.AddDays(7), facilityId);
            
            // Occupancy
            var currentOccupancy = await _accessEventRepository.GetCurrentOccupancyCountAsync(facilityId);
            dto.CheckInsToday = currentOccupancy;

            var maxOccupancy = await _dbContext.GymSettings
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.FacilityId == facilityId && !s.IsDeleted)
                .Select(s => s.MaxOccupancy)
                .FirstOrDefaultAsync();

            if (maxOccupancy > 0)
            {
                var currentVal = (decimal)currentOccupancy / maxOccupancy * 100;
                dto.OccupancyPercent = (int)Math.Min(100, currentVal);

                dto.PeakCapacityPercent.Value = Math.Min(100, currentVal);
                dto.PeakCapacityPercent.PeriodLabel = "LIVE";
                dto.PeakCapacityPercent.IsIncreasePositive = false; // Higher capacity more full

                if (dto.PeopleInsideLastHour >= 0)
                {
                    dto.PeakCapacityPercent.ComparisonValue = (decimal)dto.PeopleInsideLastHour / maxOccupancy * 100;
                    dto.PeakCapacityPercent.PercentChange = dto.PeakCapacityPercent.ComparisonValue > 0 
                        ? (dto.PeakCapacityPercent.Value - dto.PeakCapacityPercent.ComparisonValue) 
                        : 0;
                    dto.PeakCapacityPercent.ComparisonLabel = "vs last hour";
                }
            }

            // PT Upsell Rate
            var activeMemberIds = await _dbContext.Members
                .Where(m => m.FacilityId == facilityId && m.Status == MemberStatus.Active && !m.IsDeleted && m.ExpirationDate > context.UtcNow)
                .Select(m => m.MembershipPlanId)
                .ToListAsync();

            if (activeMemberIds.Any())
            {
                var ptPlanIds = await _dbContext.MembershipPlans
                    .Where(p => p.FacilityId == facilityId && p.IsPersonalTraining && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync();

                int ptCount = activeMemberIds.Count(id => id.HasValue && ptPlanIds.Contains(id.Value));
                dto.PtUpsellRate.Value = (decimal)ptCount / activeMemberIds.Count * 100;
                dto.PtUpsellRate.PeriodLabel = "CURRENT";
                dto.PtUpsellRate.ComparisonLabel = "of active members";
            }
                dto.PtUpsellRate.IsIncreasePositive = true;

            // Trial Conversion Rate (Registration Approval Rate)
            var regStats = await _dbContext.Registrations
                .IgnoreQueryFilters()
                .Where(r => r.FacilityId == facilityId && !r.IsDeleted)
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int approvedCount = regStats.FirstOrDefault(s => s.Status == RegistrationStatus.Approved)?.Count ?? 0;
            int totalRegCount = regStats.Sum(s => s.Count);

            if (totalRegCount > 0)
            {
                dto.TrialConversionRate.Value = (decimal)approvedCount / totalRegCount * 100;
                dto.TrialConversionRate.PeriodLabel = "LIFETIME";
                dto.TrialConversionRate.ComparisonLabel = "of leads converted";
            }
            dto.TrialConversionRate.IsIncreasePositive = true;

            // Membership Breakdown
            var breakdown = await _dbContext.Members
                .IgnoreQueryFilters()
                .Where(m => m.FacilityId == facilityId && m.Status == MemberStatus.Active && !m.IsDeleted && m.ExpirationDate > context.UtcNow)
                .GroupBy(m => m.MembershipPlanId)
                .Select(g => new
                {
                    PlanId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var allPlans = await _dbContext.MembershipPlans
                .IgnoreQueryFilters()
                .Where(p => p.FacilityId == facilityId && !p.IsDeleted)
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            dto.MembershipBreakdown.Clear();
            foreach (var item in breakdown)
            {
                var planName = item.PlanId.HasValue && allPlans.TryGetValue(item.PlanId.Value, out var name) ? name : "None / Walk-In";
                dto.MembershipBreakdown.Add(new PlanRevenueDto
                {
                    PlanName = planName,
                    Count = item.Count,
                    Percentage = (double)item.Count / activeMemberIds.Count * 100
                });
            }

            // Member Demographics (Sources)
            var sources = await _dbContext.Members
                .Where(m => m.FacilityId == facilityId && !m.IsDeleted)
                .GroupBy(m => m.Source)
                .Select(g => new
                {
                    Source = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();
 
            int totalCount = sources.Sum(s => s.Count);
            dto.MemberDemographics.Clear();
            foreach (var s in sources)
            {
                dto.MemberDemographics.Add(new MemberSourceDto
                {
                    Source = string.IsNullOrEmpty(s.Source) ? "Unknown" : s.Source,
                    Count = s.Count,
                    Percentage = totalCount > 0 ? (int)((double)s.Count / totalCount * 100) : 0
                });
            }

            // Gender Demographics
            var genderData = await _dbContext.Members
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.FacilityId == facilityId && !m.IsDeleted)
                .GroupBy(m => m.Gender)
                .Select(g => new { Gender = g.Key, Count = g.Count() })
                .ToListAsync();

            dto.GenderDemographics.Clear();
            foreach (var g in genderData)
            {
                dto.GenderDemographics.Add(new MemberSourceDto
                {
                    Source = g.Gender == Gender.NotSpecified ? "Unknown" : g.Gender.ToString(),
                    Count = g.Count,
                    Percentage = totalCount > 0 ? (int)((double)g.Count / totalCount * 100) : 0
                });
            }

            // Age Demographics
            var membersWithDob = await _dbContext.Members
                .Where(m => m.FacilityId == facilityId && !m.IsDeleted && m.DateOfBirth.HasValue)
                .Select(m => m.DateOfBirth.Value)
                .ToListAsync();

            var ageBuckets = new Dictionary<string, int>
            {
                { "< 18", 0 },
                { "18-25", 0 },
                { "26-35", 0 },
                { "36-50", 0 },
                { "50+", 0 }
            };

            foreach (var dob in membersWithDob)
            {
                var age = DateTime.Today.Year - dob.Year;
                if (dob > DateTime.Today.AddYears(-age)) age--;

                if (age < 18) ageBuckets["< 18"]++;
                else if (age <= 25) ageBuckets["18-25"]++;
                else if (age <= 35) ageBuckets["26-35"]++;
                else if (age <= 50) ageBuckets["36-50"]++;
                else ageBuckets["50+"]++;
            }

            dto.AgeDemographics.Clear();
            foreach (var bucket in ageBuckets.Where(b => b.Value > 0))
            {
                dto.AgeDemographics.Add(new MemberSourceDto
                {
                    Source = bucket.Key,
                    Count = bucket.Value,
                    Percentage = totalCount > 0 ? (int)((double)bucket.Value / totalCount * 100) : 0
                });
            }

            // If some members have no DOB, add them as "Unknown"
            int missingDobCount = totalCount - membersWithDob.Count;
            if (missingDobCount > 0)
            {
                dto.AgeDemographics.Add(new MemberSourceDto
                {
                    Source = "Unknown",
                    Count = missingDobCount,
                    Percentage = totalCount > 0 ? (int)((double)missingDobCount / totalCount * 100) : 0
                });
            }

            // Combined Demographics (Age Group + Gender)
            var membersForCombined = await _dbContext.Members
                .Where(m => m.FacilityId == facilityId && !m.IsDeleted && m.DateOfBirth.HasValue)
                .Select(m => new { m.DateOfBirth, m.Gender })
                .ToListAsync();

            var combinedBuckets = new Dictionary<string, DemographicBucketDto>
            {
                { "< 18", new DemographicBucketDto { AgeGroup = "< 18" } },
                { "18-25", new DemographicBucketDto { AgeGroup = "18-25" } },
                { "26-35", new DemographicBucketDto { AgeGroup = "26-35" } },
                { "36-50", new DemographicBucketDto { AgeGroup = "36-50" } },
                { "50+", new DemographicBucketDto { AgeGroup = "50+" } }
            };

            foreach (var m in membersForCombined)
            {
                var dob = m.DateOfBirth.Value;
                var age = DateTime.Today.Year - dob.Year;
                if (dob > DateTime.Today.AddYears(-age)) age--;

                string bucketKey;
                if (age < 18) bucketKey = "< 18";
                else if (age <= 25) bucketKey = "18-25";
                else if (age <= 35) bucketKey = "26-35";
                else if (age <= 50) bucketKey = "36-50";
                else bucketKey = "50+";

                if (m.Gender == Gender.Male) combinedBuckets[bucketKey].MaleCount++;
                else if (m.Gender == Gender.Female) combinedBuckets[bucketKey].FemaleCount++;
            }

            dto.CombinedDemographics = combinedBuckets.Values.ToList();
 
            // Last hour trend
            var utcOneHourAgo = context.UtcNow.AddHours(-1);
            var utcTwoHoursAgo = context.UtcNow.AddHours(-2);
            var lastHourEvents = await _accessEventRepository.GetByDateRangeAsync(facilityId, utcTwoHoursAgo, utcOneHourAgo);

            dto.PeopleInsideLastHour = lastHourEvents.Count(e => e.IsAccessGranted);
        }
    }
}
