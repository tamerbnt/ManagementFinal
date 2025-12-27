using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Exceptions;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;

namespace Management.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ProductStore _productStore;

        public ProductService(IProductRepository productRepository, ProductStore productStore)
        {
            _productRepository = productRepository;
            _productStore = productStore;
        }

        public async Task<List<ProductDto>> GetActiveProductsAsync()
        {
            var entities = await _productRepository.SearchAsync(""); // Get all

            // Filter logic usually handled by Repo for perf, but ensuring Active only here
            return entities
                .Where(p => p.IsActive)
                .Select(MapToDto)
                .ToList();
        }

        public async Task<List<InventoryDto>> GetInventoryStatusAsync()
        {
            // Back-office view includes all items (even inactive)
            var entities = await _productRepository.GetAllAsync();

            return entities.Select(p => new InventoryDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                SKU = p.SKU,
                CurrentStock = p.StockQuantity,
                ReorderPoint = p.ReorderLevel,
                LastUpdated = p.UpdatedAt ?? p.CreatedAt,
                ImageUrl = p.ImageUrl
            }).ToList();
        }

        public async Task<List<ProductDto>> SearchProductsAsync(string searchTerm, ProductCategory? category = null)
        {
            var entities = await _productRepository.SearchAsync(searchTerm, category);
            return entities.Select(MapToDto).ToList();
        }

        public async Task CreateProductAsync(ProductDto dto)
        {
            // Validation
            if (dto.Price < 0) throw new ValidationException(new Dictionary<string, string[]> { { "Price", new[] { "Price cannot be negative." } } });

            var entity = new Product
            {
                Name = dto.Name,
                SKU = dto.SKU,
                Category = dto.Category,
                Price = dto.Price,
                Cost = dto.Cost,
                StockQuantity = dto.StockQuantity,
                ReorderLevel = dto.ReorderLevel,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                IsActive = true
            };

            await _productRepository.AddAsync(entity);
            _productStore.TriggerProductAdded(MapToDto(entity));
        }

        public async Task UpdateProductAsync(ProductDto dto)
        {
            var entity = await _productRepository.GetByIdAsync(dto.Id);

            entity.Name = dto.Name;
            entity.SKU = dto.SKU;
            entity.Category = dto.Category;
            entity.Price = dto.Price;
            entity.Cost = dto.Cost;
            entity.ReorderLevel = dto.ReorderLevel;
            entity.Description = dto.Description;
            entity.ImageUrl = dto.ImageUrl;

            await _productRepository.UpdateAsync(entity);
            _productStore.TriggerProductUpdated(MapToDto(entity));
        }

        public async Task UpdateStockAsync(Guid productId, int quantityChange, string reason)
        {
            var entity = await _productRepository.GetByIdAsync(productId);

            // Allow negative change (shrinkage) but prevent negative stock if strict
            if (entity.StockQuantity + quantityChange < 0)
            {
                // Optional: Throw exception or allow backorder based on business rule
            }

            entity.StockQuantity += quantityChange;
            await _productRepository.UpdateAsync(entity);

            // Audit log for stock change would go here (e.g., _inventoryLogRepository.Add...)

            _productStore.TriggerStockUpdated(MapToDto(entity));
        }

        public async Task DeleteProductAsync(Guid id)
        {
            var entity = await _productRepository.GetByIdAsync(id);
            entity.IsActive = false; // Soft Delete / Archive
            await _productRepository.UpdateAsync(entity);

            _productStore.TriggerProductDeleted(id);
        }

        private ProductDto MapToDto(Product entity)
        {
            return new ProductDto
            {
                Id = entity.Id,
                Name = entity.Name,
                SKU = entity.SKU,
                Price = entity.Price,
                Cost = entity.Cost,
                Category = entity.Category,
                StockQuantity = entity.StockQuantity,
                ReorderLevel = entity.ReorderLevel,
                ImageUrl = entity.ImageUrl,
                Description = entity.Description
            };
        }
    }
}