using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;
using Management.Domain.Common;

namespace Management.Application.Interfaces
{
    public interface ITransactionService
    {
        Task SaveAsync(Transaction transaction);
        Task<Transaction?> GetByIdAsync(Guid id, Guid? facilityId = null);
        Task<Result<IEnumerable<Transaction>>> GetHistoryAsync(Guid facilityId);
        Task<Result<IEnumerable<Transaction>>> GetHistoryByRangeAsync(Guid facilityId, DateTime start, DateTime end);
        Task<Result> SaveAuditNoteAsync(Guid transactionId, Guid facilityId, string note);
    }
}
