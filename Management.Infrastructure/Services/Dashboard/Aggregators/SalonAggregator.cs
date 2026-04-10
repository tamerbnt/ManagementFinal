using Management.Application.DTOs;
using Management.Application.Interfaces;
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
    public class SalonAggregator : BaseAggregator
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly AppDbContext _dbContext;

        public SalonAggregator(
            IMemberRepository memberRepository,
            IAppointmentRepository appointmentRepository,
            AppDbContext dbContext)
        {
            _memberRepository = memberRepository;
            _appointmentRepository = appointmentRepository;
            _dbContext = dbContext;
        }

        public override int Priority => 20;

        public override bool CanHandle(DashboardContext context) => context.IsSalon;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;

            // Basic Demographics (Gender + Age)
            var members = await _dbContext.Members
                .Where(m => m.FacilityId == facilityId && !m.IsDeleted)
                .Select(m => new { m.Gender, m.DateOfBirth, m.Source })
                .ToListAsync();

            if (members.Any())
            {
                // 1. Gender Breakdown
                dto.GenderDemographics = members
                    .GroupBy(m => m.Gender)
                    .Select(g => new MemberSourceDto
                    {
                        Source = g.Key.ToString(),
                        Count = g.Count(),
                        Percentage = (int)((double)g.Count() / members.Count * 100)
                    })
                    .ToList();

                // 2. Age Groups
                var ages = members
                    .Where(m => m.DateOfBirth.HasValue)
                    .Select(m =>
                    {
                        var age = context.LocalToday.Year - m.DateOfBirth.Value.Year;
                        if (m.DateOfBirth.Value.Date > context.LocalToday.AddYears(-age)) age--;
                        return age;
                    })
                    .ToList();

                if (ages.Any())
                {
                    var ageGroups = new Dictionary<string, int>
                    {
                        { "Under 18", ages.Count(a => a < 18) },
                        { "18-25", ages.Count(a => a >= 18 && a <= 25) },
                        { "26-35", ages.Count(a => a >= 26 && a <= 35) },
                        { "36-50", ages.Count(a => a >= 36 && a <= 50) },
                        { "50+", ages.Count(a => a > 50) }
                    };

                    dto.AgeDemographics = ageGroups
                        .Where(kvp => kvp.Value > 0)
                        .Select(kvp => new MemberSourceDto
                        {
                            Source = kvp.Key,
                            Count = kvp.Value,
                            Percentage = (int)((double)kvp.Value / ages.Count * 100)
                        })
                        .ToList();
                }

                // 3. Acquisition Source (existing logic repurposed)
                dto.MemberDemographics = members
                    .GroupBy(m => m.Source)
                    .Select(g => new MemberSourceDto
                    {
                        Source = string.IsNullOrEmpty(g.Key) ? "Unknown" : g.Key,
                        Count = g.Count(),
                        Percentage = (int)((double)g.Count() / members.Count * 100)
                    })
                    .ToList();
            }


            dto.ActiveMembers = await _memberRepository.GetTotalCountAsync(facilityId);


            var appointments = (await _appointmentRepository.GetByDateRangeAsync(context.LocalToday, context.LocalToday.AddDays(1), facilityId))
                                .Where(a => !a.IsDeleted && a.Status != AppointmentStatus.NoShow)
                                .ToList();


            dto.TodayAppointmentsTotal = appointments.Count;
            dto.TodayAppointmentsCompleted = appointments.Count(a => a.Status == AppointmentStatus.Completed);
            dto.TodayAppointmentsPending = appointments.Count(a => a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Scheduled);
            dto.CheckInsToday = dto.TodayAppointmentsTotal; // Map to primary stat
            dto.PendingRegistrationsCount = dto.TodayAppointmentsPending; // Map to secondary stat

            // Month clients
            var localMonthEnd = context.LocalToday.AddDays(1); // Placeholder logic as in original
            var monthAppointments = (await _appointmentRepository.GetByDateRangeAsync(context.LocalToday.AddDays(-30), context.LocalToday.AddDays(1), facilityId))
                                        .Where(a => !a.IsDeleted && a.Status != AppointmentStatus.NoShow)
                                        .ToList();

            dto.ActiveClientsThisMonth = monthAppointments
                                        .Where(a => a.ClientId != Guid.Empty)
                                        .Select(a => a.ClientId)
                                        .Distinct()
                                        .Count();

            // --- Salon KPIs: Dynamic Calculations ---
            
            var thirtyDaysAgo = context.LocalToday.AddDays(-30);
            
            // 1. Fetch Sales (Products)
            var sales = await _dbContext.Sales
                .Where(s => s.FacilityId == facilityId && s.Timestamp >= thirtyDaysAgo)
                .Select(s => new { s.MemberId, s.Timestamp, Total = s.TotalAmount.Amount })
                .ToListAsync();

            // 2. Fetch Completed Appointments (Services)
            var completedMonthAppointments = monthAppointments
                .Where(a => a.Status == AppointmentStatus.Completed)
                .Select(a => new { MemberId = (Guid?)a.ClientId, Timestamp = a.StartTime, Total = a.Price })
                .ToList();

            // 3. Cluster into "Tickets" (24-hour window by Member)
            // Separate identified transactions from anonymous ones
            var identifiedTransactions = sales.Where(s => s.MemberId.HasValue).Select(s => new { s.MemberId, Date = s.Timestamp.Date, s.Total })
                .Concat(completedMonthAppointments.Where(a => a.MemberId.HasValue).Select(a => new { a.MemberId, Date = a.Timestamp.Date, a.Total }))
                .ToList();

            var anonymousTransactions = sales.Where(s => !s.MemberId.HasValue).Select(s => s.Total)
                .Concat(completedMonthAppointments.Where(a => !a.MemberId.HasValue || a.MemberId == Guid.Empty).Select(a => a.Total))
                .ToList();

            // Group identified by (Member, Date)
            var ticketGroups = identifiedTransactions
                .GroupBy(t => new { t.MemberId, t.Date })
                .Select(g => g.Sum(x => x.Total))
                .ToList();

            // Total Tickets = Identified Clusters + Each Anonymous Transaction
            var totalTicketsCount = ticketGroups.Count + anonymousTransactions.Count;
            var totalRevenue = ticketGroups.Sum() + anonymousTransactions.Sum();

            if (totalTicketsCount > 0)
            {
                dto.SalonAvgTicketValue.Value = totalRevenue / totalTicketsCount;
                dto.SalonAvgTicketValue.PeriodLabel = "30D";
                
                // For comparison (vs previous 30 days), we'd need more data, 
                // but for now let's show the real current value.
                dto.SalonAvgTicketValue.ComparisonValue = dto.SalonAvgTicketValue.Value * 0.95m; // Dummy comparison for UI feel
                dto.SalonAvgTicketValue.PercentChange = 5.0m;
                dto.SalonAvgTicketValue.ComparisonLabel = "vs prev period";
            }

            // Mock other KPIs for now as requested to focus on Avg Ticket
            dto.SalonRebookingRate.Value = 68.5m;
            dto.SalonRebookingRate.PeriodLabel = "MO";
            dto.SalonRebookingRate.ComparisonValue = 62.1m;
            dto.SalonRebookingRate.PercentChange = 10.3m;
            dto.SalonRebookingRate.ComparisonLabel = "vs last month";

            dto.SalonRetailAttachRate.Value = 24.8m;
            dto.SalonRetailAttachRate.PeriodLabel = "MO";
            dto.SalonRetailAttachRate.ComparisonValue = 21.2m;
            dto.SalonRetailAttachRate.PercentChange = 16.9m;
            dto.SalonRetailAttachRate.ComparisonLabel = "vs last month";

            dto.SalonChairUtilization.Value = 74.2m;
            dto.SalonChairUtilization.PeriodLabel = "7D";
            dto.SalonChairUtilization.ComparisonValue = 70.1m;
            dto.SalonChairUtilization.PercentChange = 5.8m;
            dto.SalonChairUtilization.ComparisonLabel = "vs last week";

        }
    }
}
