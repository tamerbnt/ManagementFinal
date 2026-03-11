using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Interfaces.App
{
    /// <summary>
    /// Provides history events for a specific facility segment (Gym, Salon, Restaurant, etc.)
    /// </summary>
    public interface IHistoryProvider
    {
        /// <summary>
        /// Gets history events for the specified range.
        /// </summary>
        Task<IEnumerable<UnifiedHistoryEventDto>> GetHistoryAsync(Guid facilityId, DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Gets the name of the segment this provider handles.
        /// </summary>
        string SegmentName { get; }
    }
}
