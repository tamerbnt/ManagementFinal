using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Services
{
    public interface IAccessEventService
    {
        /// <summary>
        /// Retrieves the most recent access logs for the Real-Time Feed.
        /// </summary>
        /// <param name="count">Number of events to retrieve (default 50).</param>
        Task<List<AccessEventDto>> GetRecentEventsAsync(int count = 50);

        /// <summary>
        /// Retrieves historical access logs within a specific date range for the History Timeline.
        /// </summary>
        Task<List<AccessEventDto>> GetEventsByRangeAsync(DateTime start, DateTime end);

        /// <summary>
        /// Calculates the current number of people inside the facility based on Entry/Exit logic.
        /// </summary>
        Task<int> GetCurrentOccupancyAsync();

        /// <summary>
        /// DEBUG ONLY: Triggers a fake card scan event to test the UI feed and hardware logic.
        /// </summary>
        /// <param name="turnstileId">Optional specific turnstile ID to simulate.</param>
        Task SimulateScanAsync(Guid? turnstileId = null);
    }
}