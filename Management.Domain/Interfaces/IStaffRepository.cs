using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface IStaffRepository : IRepository<StaffMember>
    {
        Task<StaffMember?> GetByEmailAsync(string email, Guid? facilityId = null);
        Task<StaffMember?> GetByEmailAndFacilityTypeAsync(string email, Management.Domain.Enums.FacilityType targetType);
        Task<StaffMember?> GetByCardIdAsync(string cardId, Guid? facilityId = null);
        Task<System.Collections.Generic.IEnumerable<StaffMember>> GetAllActiveAsync(Guid? facilityId = null);
        Task SafeAddAsync(StaffMember staff);
        Task<Management.Domain.Enums.FacilityType?> GetFacilityTypeByIdAsync(Guid facilityId);
        Task UpdatePinAsync(Guid staffId, string hashedPin);
        Task RestoreAsync(Guid id, Guid? facilityId = null);
        Task CleanCrossFacilityStaffAsync(IEnumerable<Guid> authorizedFacilityIds);
        Task RescueOrphanedStaffMembersAsync(Guid defaultFacilityId);
    }
}
