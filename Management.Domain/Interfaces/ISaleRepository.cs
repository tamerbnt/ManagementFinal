using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface ISaleRepository : IRepository<Sale>
    {
        // For Finance Charts & History Timeline
        Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime start, DateTime end);

        // For KPI Cards (Aggregation)
        Task<decimal> GetTotalRevenueAsync(DateTime start, DateTime end);

        // --- NEW REQUIREMENT ---
        // Required for Finance Dashboard "Failed Payments" list
        Task<IEnumerable<Sale>> GetFailedTransactionsAsync();
    }
}