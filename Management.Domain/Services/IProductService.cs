using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Enums;

namespace Management.Domain.Services
{
    public interface IProductService
    {
        /// <summary>
        /// Retrieves the public-facing catalog for the POS (Point of Sale).
        /// Filters out archived or hidden products.
        /// </summary>
        Task<List<ProductDto>> GetActiveProductsAsync();

        /// <summary>
        /// Retrieves the full inventory list for back-office management.
        /// Includes cost, reorder levels, and inactive items.
        /// </summary>
        Task<List<InventoryDto>> GetInventoryStatusAsync();

        /// <summary>
        /// Searches for products by name, SKU, or category.
        /// </summary>
        Task<List<ProductDto>> SearchProductsAsync(string searchTerm, ProductCategory? category = null);

        /// <summary>
        /// Creates a new product definition.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.ValidationException">Thrown if SKU is duplicate or Price is negative.</exception>
        Task CreateProductAsync(ProductDto product);

        /// <summary>
        /// Updates product details (Price, Category, Image).
        /// </summary>
        Task UpdateProductAsync(ProductDto product);

        /// <summary>
        /// Adjusts the stock quantity manually (e.g. new shipment, shrinkage).
        /// </summary>
        /// <param name="productId">Target Product ID.</param>
        /// <param name="quantityChange">Positive to add stock, negative to remove.</param>
        /// <param name="reason">Audit reason (e.g., "Restock", "Damage").</param>
        Task UpdateStockAsync(Guid productId, int quantityChange, string reason);

        /// <summary>
        /// Soft-deletes a product, removing it from the POS view but keeping sales history.
        /// </summary>
        Task DeleteProductAsync(Guid id);
    }
}