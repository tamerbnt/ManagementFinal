using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models.Salon;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class SalonAggregator : BaseAggregator
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IAppointmentRepository _appointmentRepository;

        public SalonAggregator(
            IMemberRepository memberRepository,
            IAppointmentRepository appointmentRepository)
        {
            _memberRepository = memberRepository;
            _appointmentRepository = appointmentRepository;
        }

        public override int Priority => 20;

        public override bool CanHandle(DashboardContext context) => context.IsSalon;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;


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
            System.IO.File.AppendAllText(@"c:\Users\techbox\.gemini\ManagementCopy\diagnostics.txt", $"[DIAG][SalonAggregator] monthAppointments rows: {(monthAppointments == null ? 0 : monthAppointments.Count)}\n");

            dto.ActiveClientsThisMonth = monthAppointments
                                        .Where(a => a.ClientId != Guid.Empty)
                                        .Select(a => a.ClientId)
                                        .Distinct()
                                        .Count();
        }
    }
}
