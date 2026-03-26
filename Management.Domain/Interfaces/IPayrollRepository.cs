using Management.Domain.Models;
using System.Threading.Tasks;

namespace Management.Domain.Interfaces
{
    public interface IPayrollRepository : IRepository<PayrollEntry>
    {
        Task<System.Collections.Generic.List<PayrollEntry>> GetByStaffIdAsync(System.Guid staffId, System.Guid? facilityId = null);
        Task<System.Collections.Generic.List<PayrollEntry>> GetByRangeAsync(System.Guid facilityId, System.DateTime start, System.DateTime end);
        Task<PayrollEntry?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null);
        Task RestoreAsync(Guid id, Guid? facilityId = null);
    }
}
