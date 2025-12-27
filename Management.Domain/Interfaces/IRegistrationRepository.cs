using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IRegistrationRepository : IRepository<Registration>
    {
        // For Inbox (RegistrationsView)
        Task<IEnumerable<Registration>> GetPendingAsync();

        // For Dashboard Badge
        Task<int> GetCountByStatusAsync(RegistrationStatus status);
    }
}