using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Repositories
{
    public class OrderRepository : Repository<RestaurantOrder>, IOrderRepository, IRestaurantOrderRepository
    {
        public OrderRepository(AppDbContext context) : base(context)
        {
        }

        public override async Task<RestaurantOrder?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            var query = _dbSet.Include(o => o.Items).Where(o => o.Id == id);
            
            if (facilityId.HasValue)
            {
                query = query.IgnoreQueryFilters().Where(o => o.FacilityId == facilityId.Value);
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<RestaurantOrder?> GetActiveOrderByTableIdAsync(Guid tableId)
        {
            return await _dbSet
                .Include(o => o.Items)
                .Where(o => o.TableId == tableId && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Paid)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<RestaurantOrder?> GetActiveOrderByTableAsync(string tableNumber)
        {
            return await _dbSet
                .Include(o => o.Items)
                .Where(o => o.TableNumber == tableNumber && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Paid)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<RestaurantOrder>> GetActiveOrdersAsync(Guid? facilityId = null)
        {
            var query = _dbSet.AsNoTracking().Include(o => o.Items)
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Paid);

            if (facilityId.HasValue)
            {
                query = query.Where(o => o.FacilityId == facilityId.Value);
            }

            return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        }

        public async Task<int> GetNextDailyOrderNumberAsync(Guid facilityId)
        {
            // Use local date for daily numbering consistency
            var today = DateTime.Today.ToUniversalTime();
            var maxNumber = await _dbSet
                .Where(o => o.FacilityId == facilityId && o.CreatedAt >= today)
                .MaxAsync(o => (int?)o.DailyOrderNumber) ?? 0;
            return maxNumber + 1;
        }

        public async Task<IEnumerable<RestaurantOrder>> GetUnpaidOrdersAsync()
        {
             return await _dbSet
                .Include(o => o.Items)
                .Where(o => o.Status != OrderStatus.Paid && o.Status != OrderStatus.Cancelled)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<RestaurantOrder>> GetTodayCompletedOrdersAsync(Guid facilityId, DateTime startDate, DateTime endDate)
        {
            return await _dbSet
                .Include(o => o.Items)
                .Where(o => o.FacilityId == facilityId && 
                            o.CompletedAt >= startDate && o.CompletedAt <= endDate &&
                            (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                .ToListAsync();
        }

        public async Task<IEnumerable<RestaurantOrder>> GetRecentOrdersAsync(Guid facilityId, int count)
        {
            return await Query()
                .Include(o => o.Items)
                .Where(o => o.FacilityId == facilityId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<RestaurantOrder>> GetOrdersByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            return await Query()
                .Include(o => o.Items)
                .Where(o => o.FacilityId == facilityId && 
                            o.CompletedAt >= start && o.CompletedAt <= end &&
                            (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                .OrderByDescending(o => o.CompletedAt)
                .ToListAsync();
        }
    }
}
