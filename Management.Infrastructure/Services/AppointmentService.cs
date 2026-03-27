using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Management.Domain.Interfaces;
using Management.Domain.Models.Salon;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Domain.Models;

namespace Management.Infrastructure.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly ITenantService _tenantService;
        private readonly IServiceScopeFactory _scopeFactory;

        public AppointmentService(
            ITenantService tenantService,
            IServiceScopeFactory scopeFactory)
        {
            _tenantService = tenantService;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Retrieves appointments for today for the current facility.
        /// </summary>
        public async Task<IEnumerable<Appointment>> GetTodayAgendaAsync(Guid facilityId)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            return await GetByRangeAsync(facilityId, today, tomorrow);
        }

        public async Task<IEnumerable<Appointment>> GetByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            using var scope = _scopeFactory.CreateScope();
            var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
            var memberRepository = scope.ServiceProvider.GetRequiredService<IMemberRepository>();
            var staffRepository = scope.ServiceProvider.GetRequiredService<IStaffRepository>();
            var dbContext = scope.ServiceProvider.GetRequiredService<Management.Infrastructure.Data.AppDbContext>();

            var reservations = await reservationRepository.GetByDateRangeAsync(start, end, facilityId);
            var appointments = new List<Appointment>();

            foreach (var r in reservations)
            {
                var member = await memberRepository.GetByIdAsync(r.MemberId);
                
                // If member is null, they were likely soft-deleted. Skip these for Recent Activity.
                if (member == null) continue;

                var staff = r.ResourceId.HasValue ? await staffRepository.GetByIdAsync(r.ResourceId.Value) : null;
                var serviceName = "Unknown Service";
                
                if (r.ServiceId.HasValue)
                {
                    var service = await dbContext.SalonServices.FindAsync(r.ServiceId.Value);
                    if (service != null) serviceName = service.Name;
                }

                appointments.Add(new Appointment
                {
                    Id = r.Id,
                    ClientId = r.MemberId,
                    ClientName = member?.FullName ?? "Unknown Client", 
                    StaffId = r.ResourceId ?? Guid.Empty,
                    StaffName = staff?.FullName ?? "Unassigned Stylist",
                    ServiceId = r.ServiceId ?? Guid.Empty,
                    ServiceName = serviceName,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    Status = MapStatus(r.Status),
                    TenantId = r.TenantId,
                    FacilityId = r.FacilityId
                });
            }

            return appointments;
        }

        private AppointmentStatus MapStatus(string status)
        {
            return status switch
            {
                "Confirmed" => AppointmentStatus.Confirmed,
                "Cancelled" => AppointmentStatus.NoShow,
                "Completed" => AppointmentStatus.Completed,
                _ => AppointmentStatus.Scheduled
            };
        }

        /// <summary>
        /// Checks if a new appointment conflicts with existing appointments for the same staff member.
        /// </summary>
        public async Task<bool> HasConflictAsync(Guid facilityId, Guid staffId, DateTime startTime, DateTime endTime, Guid? excludeAppointmentId = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();

            var existingReservations = await reservationRepository.GetByDateRangeAsync(startTime.Date, startTime.Date.AddDays(1), facilityId);

            var staffAppointments = existingReservations
                .Where(r => r.ResourceId == staffId)
                .Where(a => a.Id != excludeAppointmentId);

            return staffAppointments.Any(existing => 
                startTime < existing.EndTime && endTime > existing.StartTime);
        }

        /// <summary>
        /// Validates and books an appointment, ensuring no conflicts exist.
        /// </summary>
        public async Task<(bool Success, string Message)> BookAppointmentAsync(
            Guid facilityId,
            Guid clientId,
            Guid staffId,
            Guid serviceId,
            DateTime startTime,
            DateTime endTime)
        {
            if (await HasConflictAsync(facilityId, staffId, startTime, endTime))
            {
                return (false, "The selected staff member has a conflicting appointment at this time.");
            }

            using var scope = _scopeFactory.CreateScope();
            var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();

            var tenantId = _tenantService.GetTenantId() ?? Guid.Empty;

            var reservation = Reservation.Book(clientId, staffId, "SalonService", startTime, endTime, serviceId);
            if (reservation.IsFailure) return (false, reservation.Error.Message);

            reservation.Value.FacilityId = facilityId;
            // tenantId set if available

            await reservationRepository.AddAsync(reservation.Value);

            return (true, "Appointment booked successfully.");
        }
    }
}
