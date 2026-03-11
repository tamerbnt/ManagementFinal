using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;

namespace Management.Domain.Interfaces
{
    public interface ITableRepository : IRepository<TableModel>
    {
        Task<IEnumerable<TableModel>> GetByFacilityIdAsync(Guid facilityId);
    }
}
