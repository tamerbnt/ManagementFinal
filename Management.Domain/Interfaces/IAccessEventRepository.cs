using Management.Domain.Models;
using System.Threading.Tasks;

namespace Management.Domain.Interfaces
{
    public interface IAccessEventRepository : IRepository<AccessEvent>
    {
        Task<System.Collections.Generic.IEnumerable<AccessEvent>> GetRecentEventsAsync(System.Guid facilityId, int count);
        Task<System.Collections.Generic.IEnumerable<AccessEvent>> GetByDateRangeAsync(System.Guid facilityId, System.DateTime start, System.DateTime end);
        Task<int> GetCurrentOccupancyCountAsync(System.Guid facilityId);
        Task<int> GetVisitCountAsync(System.Guid memberId);
        Task<System.Collections.Generic.IEnumerable<AccessEvent>> GetByMemberIdAsync(System.Guid memberId);
        Task<AccessEvent?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null);
        Task<AccessEvent?> GetByTransactionIdAsync(string transactionId);
    }
}
