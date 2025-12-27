using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IPayrollRepository : IRepository<PayrollEntry>
    {
        Task<IEnumerable<PayrollEntry>> GetByStaffIdAsync(Guid staffId);
    }
}