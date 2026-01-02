using System;
using System.Collections.Generic;
using System.Linq;
using Management.Application.DTOs;

namespace Management.Application.Stores
{
    /// <summary>
    /// Holds the state of the current Point-of-Sale transaction (The Shopping Cart).
    /// Registered as a Singleton to persist state across navigation changes.
    /// Handles all financial calculations (Subtotal, Tax, Total) centrally.
    /// </summary>
    public class SaleStore
    {
        // Fired whenever items are added/removed/updated so the UI can refresh totals
        public event Action? CartChanged;

        private readonly List<CartItem> _items = new List<CartItem>();
        private const decimal TaxRate = 0.05m; // 5% Standard Tax

        // --- PUBLIC STATE ---

        /// <summary>
        /// Read-only view of current cart contents.
        /// </summary>
        public IEnumerable<CartItem> CurrentItems => _items;

        public decimal Subtotal => _items.Sum(i => i.Product.Price * i.Quantity);
        public decimal TaxAmount => Subtotal * TaxRate;
        public decimal TotalAmount => Subtotal + TaxAmount;

        public int TotalItemCount => _items.Sum(i => i.Quantity);

        // --- OPERATIONS ---

        /// <summary>
        /// Adds a product to the cart. If it exists, increments quantity.
        /// </summary>
        public void AddItem(ProductDto product, int quantity = 1)
        {
            var existing = _items.FirstOrDefault(i => i.Product.Id == product.Id);

            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                _items.Add(new CartItem(product, quantity));
            }

            OnCartChanged();
        }

        /// <summary>
        /// Adjusts the quantity of a specific line item. 
        /// Automatically removes the item if quantity drops to zero or less.
        /// </summary>
        public void UpdateQuantity(Guid productId, int delta)
        {
            var existing = _items.FirstOrDefault(i => i.Product.Id == productId);
            if (existing == null) return;

            existing.Quantity += delta;

            if (existing.Quantity <= 0)
            {
                _items.Remove(existing);
            }

            OnCartChanged();
        }

        /// <summary>
        /// Completely removes a product from the cart regardless of quantity.
        /// </summary>
        public void RemoveItem(Guid productId)
        {
            var existing = _items.FirstOrDefault(i => i.Product.Id == productId);
            if (existing != null)
            {
                _items.Remove(existing);
                OnCartChanged();
            }
        }

        /// <summary>
        /// Resets the transaction state (e.g. after successful checkout or cancellation).
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            OnCartChanged();
        }

        private void OnCartChanged()
        {
            CartChanged?.Invoke();
        }
    }

    /// <summary>
    /// Represents a line item in the shopping cart.
    /// </summary>
    public class CartItem
    {
        public ProductDto Product { get; }
        public int Quantity { get; set; }

        public decimal TotalLinePrice => Product.Price * Quantity;

        public CartItem(ProductDto product, int quantity)
        {
            Product = product;
            Quantity = quantity;
        }
    }
}