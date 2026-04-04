using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IInventoryRepository
    {
        Task AddTransactionAsync(InventoryTransaction transaction);
        Task<List<InventoryTransaction>> GetHistoryAsync(Guid facilityId, int? limit = null);
        Task<List<InventoryTransaction>> GetHistoryByProductAsync(Guid productId, Guid facilityId);
    }
}
