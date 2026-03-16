using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models.Salon;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class StaffAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;

        public StaffAggregator(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override int Priority => 30;

        public override bool CanHandle(DashboardContext context) => context.IsSalon; // Currently only Salon calculates staff perf

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;

            var localMonthEnd = context.UtcMonthStart.AddMonths(1); // Approximate for performance

            var completed = await _dbContext.Appointments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.FacilityId == facilityId && 
                            (a.TenantId == context.TenantId || a.TenantId == Guid.Empty) &&
                            !a.IsDeleted &&
                            a.StartTime >= context.UtcMonthStart && a.StartTime < context.UtcNow && 
                            a.Status == AppointmentStatus.Completed && 
                            a.StaffId != Guid.Empty)
                .Join(_dbContext.StaffMembers.AsNoTracking().IgnoreQueryFilters().Where(s => s.FacilityId == facilityId && (s.TenantId == context.TenantId || s.TenantId == Guid.Empty)), 
                      a => a.StaffId, 
                      s => s.Id, 
                      (a, s) => new { StaffId = a.StaffId, StaffName = s.FullName, ServiceId = a.ServiceId })
                .ToListAsync();

            var services = await _dbContext.SalonServices
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.FacilityId == facilityId && (s.TenantId == context.TenantId || s.TenantId == Guid.Empty))
                .ToDictionaryAsync(s => s.Id, s => s.BasePrice);


            dto.TopPerformingStaff = completed
                .GroupBy(x => x.StaffName)
                .Select(group => new StaffPerformanceDto
                {
                    StaffName = group.Key,
                    AppointmentCount = group.Count(),
                    TotalSales = group.Sum(item => services.TryGetValue(item.ServiceId, out var price) ? price : 0)
                })
                .OrderByDescending(s => s.TotalSales)
                .Take(5)
                .ToList();

        }
    }
}
