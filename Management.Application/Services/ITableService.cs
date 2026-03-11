using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;

namespace Management.Application.Services
{
    public interface ITableService
    {
        Task<IEnumerable<TableModel>> GetTablesAsync(Guid facilityId);
        Task<TableModel?> GetTableByIdAsync(Guid id);
        Task<bool> AddTableAsync(TableModel table);
        Task<bool> UpdateTableAsync(TableModel table);
        Task<bool> DeleteTableAsync(Guid id);
    }
}
