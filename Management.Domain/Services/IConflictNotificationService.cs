using System;
using System.Threading.Tasks;

namespace Management.Domain.Services
{
    /// <summary>
    /// Service for notifying the UI about sync conflicts.
    /// </summary>
    public interface IConflictNotificationService
    {
        /// <summary>
        /// Notifies the UI that a conflict has occurred and needs resolution.
        /// </summary>
        Task NotifyConflictAsync(Guid outboxMessageId);

        /// <summary>
        /// Gets the count of unresolved conflicts.
        /// </summary>
        Task<int> GetUnresolvedConflictCountAsync();
    }
}
