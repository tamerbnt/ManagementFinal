using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Application.DTOs;
using Management.Domain.Models;
using Management.Domain.Models.Salon;
using Management.Domain.Enums;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Services.Dashboard
{
    public class SalonPerformanceAggregator : BaseAggregator
    {
        private readonly AppDbContext _dbContext;

        public SalonPerformanceAggregator(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override int Priority => 15; // After basic metrics

        public override bool CanHandle(DashboardContext context)
        {
            return context.FacilityType == Management.Domain.Enums.FacilityType.Salon;
        }

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;

            // 1. Settings (Total Chairs)
            var settings = await _dbContext.SalonSettings
                .FirstOrDefaultAsync(s => s.FacilityId == facilityId) ?? new SalonSettings { TotalChairs = 5 }; // Default fallback

            // 2. Appointments context
            var appointments = await _dbContext.Appointments
                .Where(a => a.FacilityId == facilityId && a.StartTime >= context.UtcMonthStart)
                .ToListAsync();

            var completedToday = appointments.Where(a => a.StartTime >= context.UtcDayStart && a.Status == AppointmentStatus.Completed).ToList();
            var totalToday = appointments.Where(a => a.StartTime >= context.UtcDayStart).ToList();

            // 3. Rebooking Rate (Logical: Clients who completed a visit and have a future one)
            // Strategy: For appointments completed in the last 30 days, how many have a future appointment already booked?
            var completedLast30Days = await _dbContext.Appointments
                .Where(a => a.FacilityId == facilityId 
                            && a.StartTime >= DateTime.UtcNow.AddDays(-30) 
                            && a.Status == AppointmentStatus.Completed)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();

            if (completedLast30Days.Any())
            {
                var uniqueMembers = completedLast30Days;
                var futureBookingsCount = await _dbContext.Appointments
                    .Where(a => a.FacilityId == facilityId 
                                && uniqueMembers.Contains(a.ClientId) 
                                && a.StartTime > DateTime.UtcNow 
                                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed))
                    .Select(a => a.ClientId)
                    .Distinct()
                    .CountAsync();

                dto.SalonRebookingRate.Value = (decimal)futureBookingsCount / uniqueMembers.Count * 100;
                dto.SalonRebookingRate.ComparisonLabel = "vs last month";
                dto.SalonRebookingRate.PeriodLabel = "MO";
            }

            // 4. Retail Attach Rate
            // Strategy: Percentage of completed appointments today that have an associated Sale of type "Product"
            var salesToday = await _dbContext.Sales
                .Include(s => s.Items)
                .Where(s => s.FacilityId == facilityId && s.CreatedAt >= context.UtcDayStart)
                .ToListAsync();

            var appointmentSales = salesToday.Where(s => s.TransactionType == "Appointment" || s.Category == SaleCategory.Service).ToList();
            var totalCompletedWithRetail = 0;
            
            foreach(var sale in appointmentSales)
            {
                if (sale.Items.Any(i => i.ProductNameSnapshot.ToLower().Contains("product")))
                {
                    totalCompletedWithRetail++;
                }
            }

            if (totalToday.Any(a => a.Status == AppointmentStatus.Completed))
            {
                dto.SalonRetailAttachRate.Value = (decimal)totalCompletedWithRetail / totalToday.Count(a => a.Status == AppointmentStatus.Completed) * 100;
                dto.SalonRetailAttachRate.ComparisonLabel = "vs today's goal";
                dto.SalonRetailAttachRate.PeriodLabel = "LIVE";
            }

            // 5. Chair Utilization
            // Strategy: (Completed Appointments Today * Avg Duration) / (Total Chairs * 8 hours)
            if (settings.TotalChairs > 0)
            {
                var totalMinutesWorked = completedToday.Sum(a => (a.EndTime - a.StartTime).TotalMinutes);
                var totalAvailableMinutes = settings.TotalChairs * 8 * 60; // Assuming 8 hour shift
                dto.SalonChairUtilization.Value = (decimal)(totalMinutesWorked / totalAvailableMinutes) * 100;
                if (dto.SalonChairUtilization.Value > 100) dto.SalonChairUtilization.Value = 100;
                dto.SalonChairUtilization.ComparisonLabel = "vs capacity";
                dto.SalonChairUtilization.PeriodLabel = "LIVE";
            }

            // 6. Demographics
            var members = await _dbContext.Members
                .Where(m => m.FacilityId == facilityId)
                .ToListAsync();

            dto.MemberDemographics = members
                .GroupBy(m => m.Source ?? "Unknown")
                .Select(g => new MemberSourceDto
                {
                    Source = g.Key,
                    Count = g.Count(),
                    Percentage = members.Count > 0 ? (int)((double)g.Count() / members.Count * 100) : 0
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // 7. Service Profitability
            dto.ServiceProfitability = appointments
                .GroupBy(a => a.ServiceName)
                .Select(g => new SalonServiceProfitabilityDto
                {
                    ServiceName = g.Key,
                    Revenue = g.Sum(a => 50), // Fallback revenue if not linked to sale yet
                    AvgDurationMinutes = g.Average(a => (a.EndTime - a.StartTime).TotalMinutes)
                })
                .OrderByDescending(x => x.ProfitabilityIndex)
                .Take(5)
                .ToList();
                
            dto.SalonAvgTicketValue.Value = completedToday.Any() ? completedToday.Average(a => 50m) : 0; // Simple average
            dto.SalonAvgTicketValue.PeriodLabel = "LIVE";
            dto.SalonAvgTicketValue.ComparisonLabel = "avg today";
        }
    }
}
