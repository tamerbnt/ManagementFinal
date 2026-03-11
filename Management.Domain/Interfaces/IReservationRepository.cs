using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IReservationRepository : IRepository<Reservation>
    {
        Task<IEnumerable<Reservation>> GetByDateRangeAsync(DateTime start, DateTime end, Guid? facilityId = null);
        Task<IEnumerable<Reservation>> GetByMemberIdAsync(Guid memberId, Guid? facilityId = null);
        Task<Reservation?> GetByIdAsync(Guid id, Guid? facilityId = null);
    }
}
