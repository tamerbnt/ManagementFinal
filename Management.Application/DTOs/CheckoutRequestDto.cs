using System;
using System.Collections.Generic;
using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
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