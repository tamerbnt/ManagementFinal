using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface ITransactionRepository : IRepository<Transaction>
    {
        Task<IEnumerable<Transaction>> GetRecentHistoryAsync(Guid facilityId, int count);
        Task<IEnumerable<Transaction>> GetByRangeAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Transaction?> GetByIdAsync(Guid id, Guid? facilityId = null);
        Task UpdateAuditNoteAsync(Guid transactionId, string note);
    }
}
