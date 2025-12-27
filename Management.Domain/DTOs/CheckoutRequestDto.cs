using System;
using System.Collections.Generic;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    /// <summary>
    /// Represents the payload required to process a Point-of-Sale transaction.
    /// Passed from the ShopViewModel to the SaleService.
    /// </summary>
    public class CheckoutRequestDto
    {
        /// <summary>
        /// The selected payment method (Cash, Card, Account, etc.).
        /// </summary>
        public PaymentMethod Method { get; set; }

        /// <summary>
        /// The amount of money provided by the customer. 
        /// Relevant for Cash transactions to calculate change.
        /// </summary>
        public decimal AmountTendered { get; set; }

        /// <summary>
        /// Optional: The ID of the member making the purchase.
        /// Required if PaymentMethod is 'Account'.
        /// </summary>
        public Guid? MemberId { get; set; }

        /// <summary>
        /// The list of items being purchased.
        /// Key: Product ID
        /// Value: Quantity
        /// </summary>
        public Dictionary<Guid, int> Items { get; set; } = new Dictionary<Guid, int>();
    }
}