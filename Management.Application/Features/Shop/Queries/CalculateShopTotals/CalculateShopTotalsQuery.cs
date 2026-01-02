using Management.Application.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Shop.Queries.CalculateShopTotals
{
    public record CalculateShopTotalsQuery(List<CartItemDto> Items) : IRequest<ShopTotalsDto>;

    public record ShopTotalsDto
    {
        public decimal Subtotal { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public int CartItemCount { get; init; }
        public string TaxRateDisplay { get; init; } = "(5%)";
    }

    public record CartItemDto(Guid ProductId, decimal UnitPrice, int Quantity);
}
