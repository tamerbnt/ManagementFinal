using System;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IMemberRepository : IRepository<Member>
    {
        /// <summary>
        /// Filters members by name/email/ID and optional status.
        /// </summary>
        Task<System.Collections.Generic.IEnumerable<Member>> SearchAsync(string searchTerm, MemberStatus? status = null);

        /// <summary>
        /// Finds members expiring on or before the threshold date.
        /// </summary>
        Task<System.Collections.Generic.IEnumerable<Member>> GetExpiringAsync(DateTime threshold);

        // Dashboard Counters
        Task<int> GetActiveCountAsync();
        Task<int> GetTotalCountAsync();
        Task<int> GetExpiringCountAsync(DateTime threshold);
    }
}