using System;
using Management.Application.DTOs;

using Management.Domain.Interfaces;

namespace Management.Application.Stores
{
    /// <summary>
    /// Event Aggregator for Product Catalog and Inventory state.
    /// Ensures that sales in the POS tab immediately reflect in the Inventory tab,
    /// and back-office edits immediately reflect in the POS grid.
    /// </summary>
    public class ProductStore : IStateResettable
    {
        public void ResetState()
        {
            // Stateless aggregator, nothing to clear
        }
        // Fired when a new product is added to the catalog
        public event Action<ProductDto>? ProductAdded;

        // Fired when product details (Name, Price, Category) are modified
        public event Action<ProductDto>? ProductUpdated;

        // Fired specifically when stock quantities change (e.g. after a Sale or Restock)
        // Subscribers can use this to update just the StockLevel property without re-rendering the whole card.
        public event Action<ProductDto>? StockUpdated;

        // Fired when a product is soft-deleted/archived
        public event Action<Guid>? ProductDeleted;

        /// <summary>
        /// Broadcasts the addition of a new product.
        /// </summary>
        public void TriggerProductAdded(ProductDto product)
        {
            ProductAdded?.Invoke(product);
        }

        /// <summary>
        /// Broadcasts updates to product details.
        /// </summary>
        public void TriggerProductUpdated(ProductDto product)
        {
            ProductUpdated?.Invoke(product);
        }

        /// <summary>
        /// Broadcasts a change in stock levels.
        /// </summary>
        public void TriggerStockUpdated(ProductDto product)
        {
            StockUpdated?.Invoke(product);
        }

        /// <summary>
        /// Broadcasts the deletion of a product.
        /// </summary>
        public void TriggerProductDeleted(Guid productId)
        {
            ProductDeleted?.Invoke(productId);
        }
    }
}
