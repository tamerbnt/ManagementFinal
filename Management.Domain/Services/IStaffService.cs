
using Management.Domain.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Domain.Services
{
    public interface IStaffService
    {
        /// <summary>
        /// Retrieves all active staff members for the management grid.
        /// </summary>
        Task<List<StaffDto>> GetAllStaffAsync();

        /// <summary>
        /// Gets detailed information including specific permissions and history.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.EntityNotFoundException">Thrown if ID does not exist.</exception>
        Task<StaffDto> GetStaffDetailsAsync(Guid id);

        /// <summary>
        /// Registers a new staff member.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.ValidationException">Thrown if email exists or data invalid.</exception>
        Task<Guid> CreateStaffAsync(StaffDto staff);

        /// <summary>
        /// Updates staff profile details (Role, Phone, Email).
        /// </summary>
        Task UpdateStaffAsync(StaffDto staff);

        /// <summary>
        /// Revokes access and soft-deletes the staff member.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if trying to delete the last Admin.</exception>
        Task RemoveStaffAsync(Guid id);

        /// <summary>
        /// Updates the granular permission matrix for a staff member.
        /// </summary>
        Task UpdatePermissionsAsync(Guid staffId, List<PermissionDto> permissions);
    }
}