using System;
using System.Threading;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Infrastructure.Data;
using Management.Infrastructure.Data.Models;

namespace Management.Infrastructure.Services.Sync
{
    /// <summary>
    /// Defines a strategy for facility-specific data synchronization.
    /// </summary>
    public interface IFacilitySyncStrategy
    {
        /// <summary>
        /// The facility type this strategy handles.
        /// </summary>
        FacilityType FacilityType { get; }

        /// <summary>
        /// Pulls facility-specific data from the remote source.
        /// </summary>
        Task PullSpecificDataAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct);

        /// <summary>
        /// Handles facility-specific outbox messages.
        /// </summary>
        /// <returns>True if the message was handled (successfully or with a known skip), False if not handled by this strategy.</returns>
        Task<bool> HandleOutboxMessageAsync(Management.Domain.Models.OutboxMessage message, CancellationToken ct);
    }
}
