using System.Threading.Tasks;
using Management.Domain.Models;
using System.Collections.Generic; // Added for IEnumerable

namespace Management.Domain.Interfaces
{
    public interface IMembershipPlanRepository : IRepository<MembershipPlan>
    {
        Task<IEnumerable<MembershipPlan>> GetActivePlansAsync(System.Guid? facilityId = null, bool activeOnly = true);
        Task<MembershipPlan?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null);
    }
}
