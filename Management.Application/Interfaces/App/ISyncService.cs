using System.Threading.Tasks;

namespace Management.Application.Interfaces.App
{
    public enum SyncStatus
    {
        Idle,
        Syncing,
        Offline,
        Error
    }

    public interface ISyncService
    {
        Task<bool> PushChangesAsync(CancellationToken ct, Guid? facilityId = null);
        Task<bool> PullChangesAsync(CancellationToken ct, Guid? facilityId = null);
        SyncStatus Status { get; }
        event EventHandler<SyncStatus>? SyncStatusChanged;
        event EventHandler? SyncCompleted;
        
        /// <summary>
        /// Gets the count of pending outbox messages waiting to be synced.
        /// </summary>
        Task<int> GetPendingOutboxCountAsync();

        /// <summary>
        /// Resets the sync timestamp to force a full re-pull of all data.
        /// </summary>
        Task ResetSyncContextAsync();

        /// <summary>
        /// Blocks until the outbox is empty or the timeout expires.
        /// Used for "Sync Draining" during facility switches or app exit.
        /// </summary>
        Task<bool> WaitForPendingSyncAsync(CancellationToken ct);
    }
}
