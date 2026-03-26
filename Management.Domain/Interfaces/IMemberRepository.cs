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
        Task<System.Collections.Generic.IEnumerable<Member>> SearchAsync(
            string searchTerm, 
            Guid? facilityId = null,
            MemberStatus? status = null,
            Gender? gender = null,
            DateTime? joinedStart = null,
            DateTime? joinedEnd = null);

        Task<(System.Collections.Generic.IEnumerable<Member> Items, int TotalCount)> SearchPagedAsync(
            string searchTerm,
            Guid? facilityId,
            int page,
            int pageSize,
            MemberStatus? status = null,
            Gender? gender = null,
            DateTime? joinedStart = null,
            DateTime? joinedEnd = null,
            bool? isActiveFilter = null,
            DateTime? expiringBefore = null);

        Task<System.Collections.Generic.IEnumerable<Member>> GetExpiringAsync(DateTime threshold, Guid? facilityId = null);

        Task<Member?> GetByCardIdAsync(string cardId, Guid? facilityId = null);

        // Dashboard Counters
        Task<int> GetActiveCountAsync(Guid? facilityId = null);
        Task<int> GetTotalCountAsync(Guid? facilityId = null);
        Task<int> GetExpiringCountAsync(DateTime threshold, Guid? facilityId = null);
        Task RestoreAsync(Guid id, Guid? facilityId = null);
    }
}
