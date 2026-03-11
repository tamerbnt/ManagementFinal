using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Primitives;

namespace Management.Domain.Interfaces
{
    public interface IRepository<T> where T : Entity
    {
        Task<T?> GetByIdAsync(Guid id, Guid? facilityId = null);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(Guid id);
    }
}
