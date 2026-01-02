using System;

namespace Management.Domain.DTOs
{
    public record InventoryDto(
        Guid ProductId,
        string ProductName,
        string SKU,
        string ImageUrl,
        int CurrentStock,
        int ReorderPoint,
        DateTime LastUpdated
    );
}