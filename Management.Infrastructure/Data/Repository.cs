using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Common;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;

namespace Management.Infrastructure.Data
{
    /// <summary>
    /// Generic repository base class implementing common CRUD operations.
    /// </summary>
    /// <summary>
    /// Generic repository base class implementing common CRUD operations.
    /// </summary>
    public abstract class Repository<T> : IRepository<T> where T : Management.Domain.Primitives.Entity
    {
        protected readonly DbContext _context;
        protected readonly DbSet<T> _dbSet;

        protected Repository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }



        public virtual async Task<T?> GetByIdAsync(Guid id, Guid? facilityId = null)
        {
            if (facilityId.HasValue && typeof(IFacilityEntity).IsAssignableFrom(typeof(T)))
            {
                var query = _dbSet.IgnoreQueryFilters();
                // We use dynamic linq or cast to IFacilityEntity for the filter
                // For simplicity in the base class, we can use EF.Property if needed or let derived classes override
                // But a generic implementation for IFacilityEntity is better:
                return await query.FirstOrDefaultAsync(e => e.Id == id && 
                    EF.Property<Guid>(e, "FacilityId") == facilityId.Value && 
                    !e.IsDeleted);
            }
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.Where(e => !e.IsDeleted).ToListAsync();
        }

        public virtual async Task<T> AddAsync(T entity, bool saveChanges = true)
        {
            await _dbSet.AddAsync(entity);
            if (saveChanges)
            {
                await _context.SaveChangesAsync();
            }
            return entity;
        }

        public virtual async Task UpdateAsync(T entity, bool saveChanges = true)
        {
            _context.Entry(entity).State = EntityState.Modified;
            if (saveChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        public virtual async Task DeleteAsync(Guid id, bool saveChanges = true)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                if (saveChanges)
                {
                    await _context.SaveChangesAsync();
                }
            }
        }

        protected IQueryable<T> Query()
        {
            return _dbSet.Where(e => !e.IsDeleted);
        }
    }
}
