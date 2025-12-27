using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Services
{
    public interface ISaleService
    {
        /// <summary>
        /// Executes a Point-of-Sale transaction.
        /// - Validates Stock
        /// - Calculates Totals
        /// - Creates Sale/SaleItem records
        /// - Updates Inventory
        /// </summary>
        /// <param name="request">The cart contents and payment method.</param>
        /// <returns>True if successful.</returns>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if stock is insufficient.</exception>
        Task<bool> ProcessCheckoutAsync(CheckoutRequestDto request);

        /// <summary>
        /// Retrieves historical sales records for the History Timeline.
        /// </summary>
        Task<List<SaleDto>> GetSalesByRangeAsync(DateTime start, DateTime end);

        /// <summary>
        /// Retrieves the full details of a specific transaction (Receipt view).
        /// </summary>
        Task<SaleDto> GetSaleDetailsAsync(Guid saleId);
    }
}