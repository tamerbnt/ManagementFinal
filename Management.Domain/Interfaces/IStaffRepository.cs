using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IStaffRepository : IRepository<StaffMember>
    {
        Task<StaffMember?> GetByEmailAsync(string email);
        Task<System.Collections.Generic.IEnumerable<StaffMember>> GetAllActiveAsync();
    }
}