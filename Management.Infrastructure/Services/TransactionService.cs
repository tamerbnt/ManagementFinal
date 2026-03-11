using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Common;

namespace Management.Infrastructure.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger<TransactionService> _logger;

        public TransactionService(
            ITransactionRepository transactionRepository,
            ILogger<TransactionService> logger)
        {
            _transactionRepository = transactionRepository;
            _logger = logger;
        }

        public async Task SaveAsync(Transaction transaction)
        {
            try
            {
                await _transactionRepository.AddAsync(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving transaction {TransactionId}", transaction.Id);
                throw;
            }
        }

        public async Task<Transaction?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            return await _transactionRepository.GetByIdAsync(id, facilityId);
        }

        public async Task<Result<IEnumerable<Transaction>>> GetHistoryAsync(Guid facilityId)
        {
            try
            {
                var history = await _transactionRepository.GetRecentHistoryAsync(facilityId, 100);
                return Result<IEnumerable<Transaction>>.Success(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transaction history for facility {FacilityId}", facilityId);
                return Result.Failure<IEnumerable<Transaction>>(new Error("Transaction.HistoryError", "Failed to retrieve transaction history."));
            }
        }

        public async Task<Result<IEnumerable<Transaction>>> GetHistoryByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            try
            {
                var history = await _transactionRepository.GetByRangeAsync(facilityId, start, end);
                return Result<IEnumerable<Transaction>>.Success(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transaction history for range {Start} - {End} in facility {FacilityId}", start, end, facilityId);
                return Result.Failure<IEnumerable<Transaction>>(new Error("Transaction.HistoryError", "Failed to retrieve transaction history."));
            }
        }

        public async Task<Result> SaveAuditNoteAsync(Guid transactionId, Guid facilityId, string note)
        {
            try
            {
                // We use GetByIdAsync with facilityId to ensure we are updating the correct transaction
                var transaction = await _transactionRepository.GetByIdAsync(transactionId, facilityId);
                if (transaction == null)
                {
                    return Result.Failure(new Error("Transaction.NotFound", "Transaction not found in this facility."));
                }

                await _transactionRepository.UpdateAuditNoteAsync(transactionId, note);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving audit note for transaction {TransactionId} in facility {FacilityId}", transactionId, facilityId);
                return Result.Failure(new Error("Transaction.AuditNoteError", "Failed to save audit note."));
            }
        }
    }
}
