using Management.Application.DTOs;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class ClassPerformanceAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;

        public ClassPerformanceAggregator(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override int Priority => 45;

        public override bool CanHandle(DashboardContext context) => context.IsGym;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;
            var startOfToday = context.UtcDayStart;
            var endOfToday = context.UtcDayEnd;

            // Fetch classes for today
            var todayClasses = await _dbContext.GroupClasses
                .Where(c => c.FacilityId == facilityId && 
                            c.StartTime >= startOfToday && 
                            c.StartTime <= endOfToday && 
                            !c.IsDeleted)
                .ToListAsync();

            if (!todayClasses.Any()) return;

            var classIds = todayClasses.Select(c => c.Id).ToList();

            // Fetch attendance for these classes
            var attendanceCounts = await _dbContext.ClassAttendances
                .Where(a => classIds.Contains(a.GroupClassId))
                .GroupBy(a => a.GroupClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);

            var performance = new List<GymClassPerformanceDto>();
            foreach (var cls in todayClasses.OrderBy(c => c.StartTime))
            {
                int attendance = attendanceCounts.TryGetValue(cls.Id, out var count) ? count : 0;
                performance.Add(new GymClassPerformanceDto
                {
                    ClassName = cls.Name,
                    Attendance = attendance,
                    Capacity = cls.MaxCapacity
                });
            }

            dto.ClassPerformance = performance;
            
            // Overall class fill rate KPI
            if (performance.Any())
            {
                var totalAttendance = performance.Sum(p => p.Attendance);
                var totalCapacity = performance.Sum(p => p.Capacity);
                dto.ClassFillRate.Value = totalCapacity > 0 ? (decimal)totalAttendance / totalCapacity * 100 : 0;
                dto.ClassFillRate.PeriodLabel = "LIVE";
                dto.ClassFillRate.ComparisonLabel = "today's classes";
            }
        }
    }
}
