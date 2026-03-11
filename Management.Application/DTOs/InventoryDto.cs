using System;
using Management.Application.DTOs;

namespace Management.Application.DTOs
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
