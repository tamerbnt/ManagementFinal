using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Microsoft.Extensions.Logging;

namespace Management.Infrastructure.Services
{
    public class TableService : ITableService
    {
        private readonly ITableRepository _tableRepository;
        private readonly ILogger<TableService> _logger;

        public TableService(ITableRepository tableRepository, ILogger<TableService> logger)
        {
            _tableRepository = tableRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<TableModel>> GetTablesAsync(Guid facilityId)
        {
            try
            {
                return await _tableRepository.GetByFacilityIdAsync(facilityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tables for facility {FacilityId}", facilityId);
                return Array.Empty<TableModel>();
            }
        }

        public async Task<TableModel?> GetTableByIdAsync(Guid id)
        {
            try
            {
                return await _tableRepository.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table by id {TableId}", id);
                return null;
            }
        }

        public async Task<bool> AddTableAsync(TableModel table)
        {
            try
            {
                await _tableRepository.AddAsync(table);
                return true; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding table {TableLabel}", table.Label);
                return false;
            }
        }

        public async Task<bool> UpdateTableAsync(TableModel table)
        {
            try
            {
                await _tableRepository.UpdateAsync(table);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating table {TableId}", table.Id);
                return false;
            }
        }

        public async Task<bool> DeleteTableAsync(Guid id)
        {
            try
            {
                await _tableRepository.DeleteAsync(id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting table {TableId}", id);
                return false;
            }
        }
    }
}
