using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Sales.Commands.ProcessCheckout
{
    public class ProcessCheckoutCommandHandler : IRequestHandler<ProcessCheckoutCommand, Result<bool>>
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;

        public ProcessCheckoutCommandHandler(
            ISaleRepository saleRepository,
            IProductRepository productRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
        }

        public async Task<Result<bool>> Handle(ProcessCheckoutCommand request, CancellationToken cancellationToken)
        {
            var checkoutRequest = request.Request;
            if (checkoutRequest.Items == null || !checkoutRequest.Items.Any())
            {
                 return Result.Failure<bool>(new Error("Checkout.Empty", "Basket is empty."));
            }

            var paymentMethod = checkoutRequest.Method;

            var sale = Sale.Create(checkoutRequest.MemberId, paymentMethod, "Purchase");
            if (sale.IsFailure) return Result.Failure<bool>(sale.Error);

            var saleEntity = sale.Value;
            var productsToUpdate = new List<Product>();

            foreach (var item in checkoutRequest.Items)
            {
                var productId = item.Key;
                var qty = item.Value;

                var product = await _productRepository.GetByIdAsync(item.Key);
                if (product == null)
                {
                     return Result.Failure<bool>(new Error("Checkout.ProductNotFound", $"Product {item.Key} not found."));
                }

                if (product.StockQuantity < item.Value)
                {
                     return Result.Failure<bool>(new Error("Checkout.InsufficientStock", $"Insufficient stock for {product.Name}. Available: {product.StockQuantity}"));
                }

                product.UpdateStock(-qty, "Sale");
                productsToUpdate.Add(product);
                
                saleEntity.AddLineItem(product, qty);
            }

            await _saleRepository.AddAsync(saleEntity);

            foreach (var p in productsToUpdate)
            {
                await _productRepository.UpdateAsync(p);
            }

            return Result.Success(true);
        }
    }
}
