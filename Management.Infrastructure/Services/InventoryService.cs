using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;
using Management.Application.Notifications;

namespace Management.Infrastructure.Services
{
    /// <summary>
    /// Implements IInventoryService using raw SQL against AppDbContext for SQLite.
    /// Handles resource CRUD and purchase logging with lifetime cumulative totals.
    /// </summary>
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _context;
        private readonly IFacilityContextService _facilityContext;
        private readonly ILogger<InventoryService> _logger;
        private readonly MediatR.IMediator _mediator;

        public InventoryService(
            AppDbContext context,
            IFacilityContextService facilityContext,
            ILogger<InventoryService> logger,
            MediatR.IMediator mediator)
        {
            _context = context;
            _facilityContext = facilityContext;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<IEnumerable<InventoryResourceDto>> GetResourcesAsync(Guid facilityId)
        {
            try
            {
                var facilityIdStr = facilityId.ToString();
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                var results = new List<InventoryResourceDto>();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT r.id, r.name, r.unit, r.tenant_id,
                           COALESCE(SUM(p.quantity), 0) AS cumulative_total
                    FROM inventory_resources r
                    LEFT JOIN inventory_purchases p ON p.resource_id = r.id
                    WHERE r.facility_id = @facilityId
                    GROUP BY r.id, r.name, r.unit, r.tenant_id
                    ORDER BY r.name ASC";

                var param = cmd.CreateParameter();
                param.ParameterName = "@facilityId";
                param.Value = facilityIdStr;
                cmd.Parameters.Add(param);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new InventoryResourceDto
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        Name = reader.GetString(1),
                        Unit = reader.GetString(2),
                        TenantId = Guid.TryParse(reader.GetString(3), out var tid) ? tid : Guid.Empty,
                        CumulativeTotal = reader.GetDecimal(4),
                        FacilityId = facilityId
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inventory resources for facility {FacilityId}", facilityId);
                return Array.Empty<InventoryResourceDto>();
            }
            finally
            {
                // Only close if it was opened above (EF may manage the connection)
                if (_context.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                    await _context.Database.GetDbConnection().CloseAsync();
            }
        }

        public async Task<bool> AddResourceAsync(InventoryResourceDto dto)
        {
            try
            {
                var id = dto.Id.ToString();
                var now = DateTime.UtcNow.ToString("o");
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO inventory_resources (id, tenant_id, facility_id, name, unit, created_at, updated_at) VALUES ({0},{1},{2},{3},{4},{5},{5})",
                    id, dto.TenantId.ToString(), dto.FacilityId.ToString(), dto.Name, dto.Unit, now);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding inventory resource {Name}", dto.Name);
                return false;
            }
        }

        public async Task<bool> DeleteResourceAsync(Guid resourceId)
        {
            try
            {
                var idStr = resourceId.ToString();
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM inventory_purchases WHERE resource_id = {0}", idStr);
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM inventory_resources WHERE id = {0}", idStr);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory resource {ResourceId}", resourceId);
                return false;
            }
        }

        public async Task<IEnumerable<InventoryPurchaseDto>> GetPurchaseHistoryAsync(Guid facilityId)
        {
            try
            {
                var facilityIdStr = facilityId.ToString();
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                var results = new List<InventoryPurchaseDto>();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT p.id, p.resource_id, r.name, r.unit, p.quantity, p.date, p.note, p.tenant_id, p.total_price, p.unit_price
                    FROM inventory_purchases p
                    JOIN inventory_resources r ON r.id = p.resource_id
                    WHERE p.facility_id = @facilityId
                    ORDER BY p.date DESC, p.created_at DESC";

                var param = cmd.CreateParameter();
                param.ParameterName = "@facilityId";
                param.Value = facilityIdStr;
                cmd.Parameters.Add(param);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new InventoryPurchaseDto
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        ResourceId = Guid.Parse(reader.GetString(1)),
                        ResourceName = reader.GetString(2),
                        Unit = reader.GetString(3),
                        Quantity = reader.GetDecimal(4),
                        Date = DateTime.Parse(reader.GetString(5)),
                        Note = reader.IsDBNull(6) ? null : reader.GetString(6),
                        TenantId = Guid.TryParse(reader.GetString(7), out var tid) ? tid : Guid.Empty,
                        TotalPrice = reader.GetDecimal(8),
                        UnitPrice = reader.GetDecimal(9),
                        FacilityId = facilityId
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching purchase history for facility {FacilityId}", facilityId);
                return Array.Empty<InventoryPurchaseDto>();
            }
            finally
            {
                if (_context.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                    await _context.Database.GetDbConnection().CloseAsync();
            }
        }

        public async Task<IEnumerable<InventoryPurchaseDto>> GetPurchasesByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            try
            {
                var facilityIdStr = facilityId.ToString();
                var startDateStr = start.ToLocalTime().ToString("yyyy-MM-dd");
                var endDateStr = end.ToLocalTime().ToString("yyyy-MM-dd");
                
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                var results = new List<InventoryPurchaseDto>();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT p.id, p.resource_id, r.name, r.unit, p.quantity, p.date, p.note, p.tenant_id, p.total_price, p.unit_price
                    FROM inventory_purchases p
                    JOIN inventory_resources r ON r.id = p.resource_id
                    WHERE p.facility_id = @facilityId AND p.date >= @start AND p.date <= @end
                    ORDER BY p.date DESC, p.created_at DESC";

                var pFac = cmd.CreateParameter();
                pFac.ParameterName = "@facilityId"; pFac.Value = facilityIdStr;
                cmd.Parameters.Add(pFac);

                var pStart = cmd.CreateParameter();
                pStart.ParameterName = "@start"; pStart.Value = startDateStr;
                cmd.Parameters.Add(pStart);

                var pEnd = cmd.CreateParameter();
                pEnd.ParameterName = "@end"; pEnd.Value = endDateStr;
                cmd.Parameters.Add(pEnd);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new InventoryPurchaseDto
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        ResourceId = Guid.Parse(reader.GetString(1)),
                        ResourceName = reader.GetString(2),
                        Unit = reader.GetString(3),
                        Quantity = reader.GetDecimal(4),
                        Date = DateTime.Parse(reader.GetString(5)),
                        Note = reader.IsDBNull(6) ? null : reader.GetString(6),
                        TenantId = Guid.TryParse(reader.GetString(7), out var tid) ? tid : Guid.Empty,
                        TotalPrice = reader.GetDecimal(8),
                        UnitPrice = reader.GetDecimal(9),
                        FacilityId = facilityId
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching purchases by range for facility {FacilityId}", facilityId);
                return Array.Empty<InventoryPurchaseDto>();
            }
            finally
            {
                if (_context.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                    await _context.Database.GetDbConnection().CloseAsync();
            }
        }

        public async Task<bool> LogPurchaseAsync(InventoryPurchaseDto dto)
        {
            try
            {
                var id = dto.Id.ToString();
                var now = DateTime.UtcNow.ToString("o");
                var dateStr = dto.Date.ToString("yyyy-MM-dd");
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO inventory_purchases (id, tenant_id, facility_id, resource_id, quantity, total_price, unit_price, date, note, created_at) VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8},{9})",
                    id, dto.TenantId.ToString(), dto.FacilityId.ToString(), dto.ResourceId.ToString(),
                    dto.Quantity, dto.TotalPrice, dto.UnitPrice, dateStr, dto.Note ?? "", now);
                
                await _mediator.Publish(new FacilityActionCompletedNotification(
                    dto.FacilityId,
                    "InventoryPurchase",
                    dto.ResourceName,
                    $"Logged purchase of {dto.Quantity} {dto.Unit} for {dto.TotalPrice:C}"));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging purchase for resource {ResourceId}", dto.ResourceId);
                return false;
            }
        }
    }
}
