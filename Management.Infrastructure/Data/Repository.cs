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
            // ISOLATION: In a long-lived WPF DbContext, another instance might already be tracked.
            // If we fetch without detaching, EF Core returns the stale tracked instance.
            // Detaching first ensures we get a fresh, clean entity from the database.
            var tracked = _context.ChangeTracker.Entries<T>()
                .FirstOrDefault(e => e.Entity.Id == id);
            
            if (tracked != null)
            {
                tracked.State = EntityState.Detached;
            }

            if (facilityId.HasValue && typeof(IFacilityEntity).IsAssignableFrom(typeof(T)))
            {
                var query = _dbSet.IgnoreQueryFilters();
                return await query.FirstOrDefaultAsync(e => e.Id == id && 
                    EF.Property<Guid>(e, "FacilityId") == facilityId.Value);
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
            _dbSet.Update(entity);
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

        public virtual async Task RestoreAsync(Guid id, Guid? facilityId = null)
        {
            var query = _dbSet.IgnoreQueryFilters();
            if (facilityId.HasValue && typeof(IFacilityEntity).IsAssignableFrom(typeof(T)))
            {
                query = query.Where(e => EF.Property<Guid>(e, "FacilityId") == facilityId.Value);
            }

            var entity = await query.FirstOrDefaultAsync(e => e.Id == id);
            if (entity != null)
            {
                entity.Restore();
                await _context.SaveChangesAsync();
            }
        }

        public virtual async Task ReloadAsync(T entity)
        {
            await _context.Entry(entity).ReloadAsync();
        }

        protected IQueryable<T> Query()
        {
            return _dbSet.Where(e => !e.IsDeleted);
        }
    }
}
