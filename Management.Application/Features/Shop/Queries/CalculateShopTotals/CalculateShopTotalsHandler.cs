using Management.Application.Features.Shop.Queries.CalculateShopTotals;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Features.Shop.Queries.CalculateShopTotals
{
    public class CalculateShopTotalsHandler : IRequestHandler<CalculateShopTotalsQuery, ShopTotalsDto>
    {
        private const decimal TaxRate = 0.05m;

        public Task<ShopTotalsDto> Handle(CalculateShopTotalsQuery request, CancellationToken cancellationToken)
        {
            var subtotal = request.Items.Sum(x => x.UnitPrice * x.Quantity);
            var taxAmount = subtotal * TaxRate;
            var totalCount = request.Items.Sum(x => x.Quantity);

            return Task.FromResult(new ShopTotalsDto
            {
                Subtotal = subtotal,
                TaxAmount = taxAmount,
                TotalAmount = subtotal + taxAmount,
                CartItemCount = totalCount,
                TaxRateDisplay = $"({TaxRate:P0})"
            });
        }
    }
}
