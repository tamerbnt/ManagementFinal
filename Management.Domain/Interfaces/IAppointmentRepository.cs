using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Salon;

namespace Management.Domain.Interfaces
{
    public interface IAppointmentRepository : IRepository<Appointment>
    {
        Task<IEnumerable<Appointment>> GetByDateRangeAsync(DateTime start, DateTime end, Guid? facilityId = null);
        Task<IEnumerable<Appointment>> GetByStaffAsync(Guid staffId, DateTime start, DateTime end, Guid? facilityId = null);
        Task<bool> HasConflictAsync(Guid staffId, DateTime start, DateTime end, Guid? excludingAppointmentId = null, Guid? facilityId = null);
    }
}
