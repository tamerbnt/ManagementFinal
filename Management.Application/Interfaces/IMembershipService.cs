using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Application.Interfaces
{
    public interface IMembershipService
    {
        Task<List<MembershipPlan>> GetAllPlansAsync();
        Task SavePlanAsync(MembershipPlan plan, List<Guid> selectedFacilityIds);
        Task<List<Facility>> GetAllFacilitiesAsync();
    }
}
