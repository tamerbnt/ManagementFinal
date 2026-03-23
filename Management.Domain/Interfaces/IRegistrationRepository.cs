using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IRegistrationRepository : IRepository<Registration>
    {
        // For Inbox (RegistrationsView)
        Task<IEnumerable<Registration>> GetPendingRegistrationsAsync(System.Guid? facilityId = null);
        Task<int> GetCountByStatusAsync(RegistrationStatus status, System.Guid? facilityId = null);
        Task<Registration?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null);
        
        Task<(System.Collections.Generic.IEnumerable<Registration> Items, int TotalCount)> SearchPagedAsync(
            string searchTerm,
            System.Guid? facilityId,
            int page,
            int pageSize,
            RegistrationStatus? status = null,
            RegistrationFilterType? filterType = null);
        Task RestoreAsync(System.Guid id);
    }
}
