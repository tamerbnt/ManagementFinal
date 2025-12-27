using System;
using System.Collections.Generic;
using System.Linq; // Added for Skip/Take
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Domain.Interfaces; // Keeping consistency with previous step
using Management.Domain.Models;
using Management.Infrastructure.Data;
using Management.Domain.Exceptions;

namespace Management.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : Entity
    {
        protected readonly GymDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(GymDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.AsNoTracking().ToListAsync();
        }

        public virtual async Task<T> GetByIdAsync(Guid id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity == null)
            {
                throw new EntityNotFoundException(typeof(T).Name, id);
            }
            return entity;
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }

            _context.Entry(entity).State = EntityState.Modified;

            // REMOVED: Manual UpdatedAt (Handled by DbContext Interceptor)

            await _context.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            var entity = await GetByIdAsync(id);
            _dbSet.Remove(entity); // Triggers Soft Delete via Context Interceptor
            await _context.SaveChangesAsync();
        }
    }
}