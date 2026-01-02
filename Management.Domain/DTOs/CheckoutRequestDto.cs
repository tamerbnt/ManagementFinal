using System;
using System.Collections.Generic;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    /// <summary>
    /// Represents the payload required to process a Point-of-Sale transaction.
    /// Passed from the ShopViewModel to the SaleService.
    /// </summary>
    public record CheckoutRequestDto(
        PaymentMethod Method,
        decimal AmountTendered,
        Guid? MemberId,
        IReadOnlyDictionary<Guid, int> Items
    );
}