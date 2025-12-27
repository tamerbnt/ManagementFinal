using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Exceptions;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Application.Services
{
    public class StaffService : IStaffService
    {
        private readonly IStaffRepository _staffRepository;

        public StaffService(IStaffRepository staffRepository)
        {
            _staffRepository = staffRepository;
        }

        public async Task<List<StaffDto>> GetAllStaffAsync()
        {
            var entities = await _staffRepository.GetAllActiveAsync();
            return entities.Select(MapToDto).ToList();
        }

        public async Task<StaffDto> GetStaffDetailsAsync(Guid id)
        {
            var entity = await _staffRepository.GetByIdAsync(id);
            return MapToDto(entity);
        }

        public async Task<Guid> CreateStaffAsync(StaffDto dto)
        {
            // 1. Validation: Email Uniqueness
            var existing = await _staffRepository.GetByEmailAsync(dto.Email);
            if (existing != null)
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    { "Email", new[] { "Email address is already in use." } }
                });

            // 2. Map & Create
            var entity = new StaffMember
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Role = dto.Role,
                HireDate = DateTime.UtcNow,
                IsActive = true,
                PinCode = "1234" // Default/Random for V1, would be hashed in V2
            };

            await _staffRepository.AddAsync(entity);
            return entity.Id;
        }

        public async Task UpdateStaffAsync(StaffDto dto)
        {
            var entity = await _staffRepository.GetByIdAsync(dto.Id);

            entity.FullName = dto.FullName;
            entity.Email = dto.Email;
            entity.PhoneNumber = dto.PhoneNumber;
            entity.Role = dto.Role;

            await _staffRepository.UpdateAsync(entity);
        }

        public async Task RemoveStaffAsync(Guid id)
        {
            var target = await _staffRepository.GetByIdAsync(id);

            // Business Rule: Cannot delete the last Admin
            if (target.Role == StaffRole.Admin)
            {
                var allStaff = await _staffRepository.GetAllActiveAsync();
                var adminCount = allStaff.Count(s => s.Role == StaffRole.Admin);

                if (adminCount <= 1)
                {
                    throw new BusinessRuleViolationException("Cannot remove the only Administrator.");
                }
            }

            // Soft Delete via Repository
            await _staffRepository.DeleteAsync(id);
        }

        public async Task UpdatePermissionsAsync(Guid staffId, List<PermissionDto> permissions)
        {
            // In V1, permissions are derived from Role (Enum). 
            // In V2, we would serialize this list to a JSON blob or related table.
            // For now, we assume Role update is handled via UpdateStaffAsync.
            await Task.CompletedTask;
        }

        private StaffDto MapToDto(StaffMember entity)
        {
            return new StaffDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
                Role = entity.Role,
                HireDate = entity.HireDate,
                Status = entity.IsActive ? "Active" : "Inactive",
                // Permissions are generated dynamically based on Role in the AuthenticationService
                // or mapped here if stored in DB.
                Permissions = new List<PermissionDto>()
            };
        }
    }
}