using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IAccessEventRepository : IRepository<AccessEvent>
    {
        // For Real-Time Feed (limited by count, e.g., last 50)
        Task<IEnumerable<AccessEvent>> GetRecentEventsAsync(int count);

        // For History View (Timeline)
        Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(DateTime start, DateTime end);

        // For Dashboard (Hero Card)
        // Calculates current "People Inside" (Entries - Exits today)
        Task<int> GetCurrentOccupancyCountAsync();
    }
}