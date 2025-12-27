using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IReservationRepository : IRepository<Reservation>
    {
        Task<IEnumerable<Reservation>> GetByDateRangeAsync(DateTime start, DateTime end);
        Task<IEnumerable<Reservation>> GetByMemberIdAsync(Guid memberId);
    }
}