using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Salon;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IAppointmentService
    {
        Task<IEnumerable<Appointment>> GetTodayAgendaAsync(Guid facilityId);
        Task<IEnumerable<Appointment>> GetByRangeAsync(Guid facilityId, DateTime start, DateTime end);
        Task<bool> HasConflictAsync(Guid facilityId, Guid staffId, DateTime startTime, DateTime endTime, Guid? excludeAppointmentId = null);
        Task<(bool Success, string Message)> BookAppointmentAsync(
            Guid facilityId,
            Guid clientId,
            Guid staffId,
            Guid serviceId,
            DateTime startTime,
            DateTime endTime);
    }
}
