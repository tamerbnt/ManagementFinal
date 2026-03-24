using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface ISaleRepository : IRepository<Sale>
    {
        // For Finance Charts & History Timeline
        Task<IEnumerable<Sale>> GetByDateRangeAsync(Guid facilityId, DateTime start, DateTime end);

        // For KPI Cards (Aggregation)
        Task<decimal> GetTotalRevenueAsync(Guid facilityId, DateTime start, DateTime end);

        // --- NEW REQUIREMENT ---
        // Required for Finance Dashboard "Failed Payments" list
        Task<IEnumerable<Sale>> GetFailedTransactionsAsync(Guid facilityId);
        
        Task<IEnumerable<Sale>> GetSalesByMemberAsync(Guid memberId, Guid facilityId);
        Task<IEnumerable<Sale>> GetSalesByMemberForUndoAsync(Guid memberId, Guid facilityId);
        Task RestoreAsync(Guid id);
    }
}
