using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Salon;

namespace Management.Domain.Interfaces
{
    public interface ISalonServiceRepository : IRepository<SalonService>
    {
        Task<IEnumerable<SalonService>> GetAllAsync(Guid facilityId);
        Task RestoreAsync(Guid id, Guid? facilityId = null);
    }
}
