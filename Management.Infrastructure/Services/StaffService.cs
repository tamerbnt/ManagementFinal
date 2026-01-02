using Management.Application.Features.Staff.Queries.GetStaff;
using Management.Application.Services;
using Management.Application.Features.Staff.Commands.CreateStaff;
using Management.Application.Services;
using Management.Application.Features.Staff.Commands.UpdateStaff;
using Management.Application.Services;
using Management.Application.Features.Staff.Commands.TerminateStaff;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Infrastructure.Services
{
    public class StaffService : IStaffService
    {
        private readonly ISender _sender;

        public StaffService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<IEnumerable<Management.Domain.Models.StaffMember>> GetAllAsync()
        {
            // Return mock data for salon staff selection using factory method
            var email = Management.Domain.ValueObjects.Email.Create("staff@example.com").Value;
            var phone = Management.Domain.ValueObjects.PhoneNumber.Create("555-0100").Value;

            var results = new List<Management.Domain.Models.StaffMember>();
            
            var elena = Management.Domain.Models.StaffMember.Recruit("Elena Gilbert", email, phone, Management.Domain.Enums.StaffRole.Trainer);
            if (elena.IsSuccess) results.Add(elena.Value);
            
            var damon = Management.Domain.Models.StaffMember.Recruit("Damon Salvatore", email, phone, Management.Domain.Enums.StaffRole.Trainer);
            if (damon.IsSuccess) results.Add(damon.Value);
            
            var bonnie = Management.Domain.Models.StaffMember.Recruit("Bonnie Bennett", email, phone, Management.Domain.Enums.StaffRole.Trainer);
            if (bonnie.IsSuccess) results.Add(bonnie.Value);
            
            var caroline = Management.Domain.Models.StaffMember.Recruit("Caroline Forbes", email, phone, Management.Domain.Enums.StaffRole.Trainer);
            if (caroline.IsSuccess) results.Add(caroline.Value);

            return results;
        }

        public async Task<Result<List<StaffDto>>> GetAllStaffAsync()
        {
            return await _sender.Send(new GetAllStaffQuery());
        }

        public async Task<Result<StaffDto>> GetStaffDetailsAsync(Guid id)
        {
            return await _sender.Send(new GetStaffQuery(id));
        }

        public async Task<Result<Guid>> CreateStaffAsync(StaffDto staff)
        {
            return await _sender.Send(new CreateStaffCommand(staff));
        }

        public async Task<Result> UpdateStaffAsync(StaffDto staff)
        {
            return await _sender.Send(new UpdateStaffCommand(staff));
        }

        public async Task<Result> RemoveStaffAsync(Guid id)
        {
            return await _sender.Send(new TerminateStaffCommand(id));
        }

        public async Task<Result<List<string>>> GetAvailableFacilitiesForStaffAsync(Guid staffId)
        {
            // Mock: Admin has access to all, others may vary
            return Result.Success(new List<string> { "Gym", "Salon", "Restaurant" });
        }

        public async Task<Result> UpdatePermissionsAsync(Guid staffId, List<PermissionDto> permissions)
        {
            return Result.Success();
        }
    }
}
