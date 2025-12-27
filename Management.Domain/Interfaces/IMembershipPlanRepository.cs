using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IMembershipPlanRepository : IRepository<MembershipPlan>
    {
        Task<System.Collections.Generic.IEnumerable<MembershipPlan>> GetActivePlansAsync();
    }
}