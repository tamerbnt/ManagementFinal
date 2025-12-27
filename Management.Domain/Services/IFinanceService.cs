using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Services
{
    public interface IFinanceService
    {
        /// <summary>
        /// Aggregates data from Sales, Members, and Expenses to build the Finance Dashboard.
        /// </summary>
        Task<FinancialMetricsDto> GetDashboardMetricsAsync();

        /// <summary>
        /// Retrieves a list of recent failed transactions for the "Action Required" list.
        /// </summary>
        Task<List<FailedPaymentDto>> GetFailedPaymentsAsync();

        /// <summary>
        /// Attempts to re-process a specific failed transaction via the gateway.
        /// </summary>
        Task<bool> RetryPaymentAsync(Guid paymentId);
    }
}