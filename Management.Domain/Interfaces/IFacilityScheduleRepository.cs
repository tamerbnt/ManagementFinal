using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IFacilityScheduleRepository : IRepository<FacilitySchedule>
    {
        Task<IEnumerable<FacilitySchedule>> GetByFacilityIdAsync(Guid facilityId);
    }
}
