using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Exceptions;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;

namespace Management.Application.Services
{
    public class SaleService : ISaleService
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly ProductStore _productStore;

        public SaleService(
            ISaleRepository saleRepository,
            IProductRepository productRepository,
            ProductStore productStore)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _productStore = productStore;
        }

        public async Task<bool> ProcessCheckoutAsync(CheckoutRequestDto request)
        {
            if (request.Items == null || !request.Items.Any()) return false;

            var sale = new Sale
            {
                Timestamp = DateTime.UtcNow,
                PaymentMethod = request.Method,
                MemberId = request.MemberId,
                TransactionType = "Purchase"
            };

            decimal calculatedTotal = 0;
            var productsToUpdate = new List<Product>();

            // 1. Process Items & Validate Stock
            foreach (var item in request.Items)
            {
                var productId = item.Key;
                var qty = item.Value;

                var product = await _productRepository.GetByIdAsync(productId);

                // Business Rule: Check Stock
                if (product.StockQuantity < qty)
                {
                    throw new BusinessRuleViolationException($"Insufficient stock for {product.Name}. Available: {product.StockQuantity}");
                }

                // Create Line Item with SNAPSHOT values
                var lineItem = new SaleItem
                {
                    ProductId = product.Id,
                    ProductNameSnapshot = product.Name,
                    UnitPriceSnapshot = product.Price, // Captures price at this moment
                    Quantity = qty
                };

                calculatedTotal += lineItem.TotalLinePrice;
                sale.Items.Add(lineItem);

                // Decrement Stock
                product.StockQuantity -= qty;
                productsToUpdate.Add(product);
            }

            // 2. Finalize Header
            sale.SubtotalAmount = calculatedTotal;
            sale.TaxAmount = calculatedTotal * 0.05m; // 5% Tax Logic (should match Store logic)
            sale.TotalAmount = sale.SubtotalAmount + sale.TaxAmount;

            // 3. Persist (Transactional ideally, EF Core SaveChanges is atomic)
            await _saleRepository.AddAsync(sale);

            // 4. Update Inventory & Notify UI
            foreach (var p in productsToUpdate)
            {
                // We update products individually or in batch if Repo supports it.
                // EF Core Context tracks these entities since we fetched them, 
                // so simply calling SaveChanges on the context would persist them.
                // Since _saleRepository and _productRepository share the same Context (Scoped),
                // we should technically call Update on them.
                await _productRepository.UpdateAsync(p);

                // Notify UI (Shop Tile turns yellow if low stock)
                _productStore.TriggerStockUpdated(new ProductDto
                {
                    Id = p.Id,
                    StockQuantity = p.StockQuantity,
                    ReorderLevel = p.ReorderLevel
                });
            }

            return true;
        }

        public async Task<List<SaleDto>> GetSalesByRangeAsync(DateTime start, DateTime end)
        {
            var sales = await _saleRepository.GetByDateRangeAsync(start, end);
            return sales.Select(s => new SaleDto
            {
                Id = s.Id,
                Timestamp = s.Timestamp,
                TotalAmount = s.TotalAmount,
                PaymentMethod = s.PaymentMethod.ToString(),
                TransactionType = s.TransactionType
            }).ToList();
        }

        public async Task<SaleDto> GetSaleDetailsAsync(Guid saleId)
        {
            var sale = await _saleRepository.GetByIdAsync(saleId);
            return new SaleDto
            {
                Id = sale.Id,
                Timestamp = sale.Timestamp,
                TotalAmount = sale.TotalAmount,
                PaymentMethod = sale.PaymentMethod.ToString(),
                TransactionType = sale.TransactionType
            };
        }
    }
}