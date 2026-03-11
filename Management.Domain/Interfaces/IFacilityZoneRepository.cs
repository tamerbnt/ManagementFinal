using Management.Domain.Models;
using System.Threading.Tasks;

namespace Management.Domain.Interfaces
{
    public interface IFacilityZoneRepository : IRepository<FacilityZone>
    {
        Task<FacilityZone?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null);
    }
}
