using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

namespace Management.Application.Services
{
    public interface IFinanceService
    {
        /// <summary>
        /// Aggregates data from Sales, Members, and Expenses to build the Finance Dashboard.
        /// </summary>
        Task<Result<FinancialMetricsDto>> GetDashboardMetricsAsync(Guid facilityId);

        /// <summary>
        /// Retrieves a list of recent failed transactions for the "Action Required" list.
        /// </summary>
        Task<Result<List<FailedPaymentDto>>> GetFailedPaymentsAsync(Guid facilityId);

        /// <summary>
        /// Attempts to re-process a specific failed transaction via the gateway.
        /// </summary>
        Task<Result> RetryPaymentAsync(Guid facilityId, Guid paymentId);
    }
}