using System;
using Management.Application.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IAccessEventService
    {
        /// <summary>
        /// Retrieves the most recent access logs for the Real-Time Feed.
        /// </summary>
        /// <param name="count">Number of events to retrieve (default 50).</param>
        Task<Result<List<AccessEventDto>>> GetRecentEventsAsync(Guid facilityId, int count = 50);

        /// <summary>
        /// Retrieves historical access logs within a specific date range for the History Timeline.
        /// </summary>
        Task<Result<List<AccessEventDto>>> GetEventsByRangeAsync(Guid facilityId, DateTime start, DateTime end);

        /// <summary>
        /// Calculates the current number of people inside the facility based on Entry/Exit logic.
        /// </summary>
        Task<Result<int>> GetCurrentOccupancyAsync(Guid facilityId);

        /// <summary>
        /// DEBUG ONLY: Triggers a fake card scan event to test the UI feed and hardware logic.
        /// </summary>
        /// <param name="turnstileId">Optional specific turnstile ID to simulate.</param>
        Task<Result> SimulateScanAsync(Guid facilityId, Guid? turnstileId = null);

        Task<Result<AccessEventDto>> ValidateAccessRequestAsync(string cardId, Guid facilityId, ScanDirection direction, string? transactionId = null);
        Task<Result<AccessEventDto>> CommitAccessRequestAsync(string cardId, Guid facilityId, ScanDirection direction, string? transactionId = null);
    }
}
