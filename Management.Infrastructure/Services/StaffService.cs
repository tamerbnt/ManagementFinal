using Management.Application.Features.Staff.Queries.GetStaff;
using Management.Application.Services;
using Management.Application.Features.Staff.Commands.CreateStaff;
using Management.Application.Features.Staff.Commands.UpdateStaff;
using Management.Application.Features.Staff.Commands.TerminateStaff;
using Management.Application.Features.Staff.Commands.RestoreStaff;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services
{
    public class StaffService : IStaffService
    {
        private readonly ISender _sender;
        private readonly IServiceScopeFactory _scopeFactory;

        public StaffService(ISender sender, IServiceScopeFactory scopeFactory)
        {
            _sender = sender;
            _scopeFactory = scopeFactory;
        }

        public async Task<IEnumerable<Management.Domain.Models.StaffMember>> GetAllAsync()
        {
            var result = await _sender.Send(new GetAllStaffQuery());
            if (result is not { IsSuccess: true, Value: var staffList })
            {
                return Enumerable.Empty<Management.Domain.Models.StaffMember>();
            }

            var staffMembers = new List<Management.Domain.Models.StaffMember>();
            foreach (var dto in staffList)
            {
                try
                {
                    var emailResult = Management.Domain.ValueObjects.Email.Create(dto.Email);
                    var phoneResult = Management.Domain.ValueObjects.PhoneNumber.Create(dto.PhoneNumber);
                    
                    if (emailResult.IsFailure) continue;

                    var recruitResult = Management.Domain.Models.StaffMember.Recruit(
                        dto.TenantId,
                        dto.FacilityId,
                        dto.FullName,
                        emailResult.Value,
                        phoneResult.IsSuccess ? phoneResult.Value : Management.Domain.ValueObjects.PhoneNumber.None,
                        (Management.Domain.Enums.StaffRole)dto.Role,
                        dto.Salary > 0 ? dto.Salary : 2000,
                        dto.PaymentDay > 0 ? dto.PaymentDay : 1
                    );

                    if (recruitResult.IsSuccess)
                    {
                        staffMembers.Add(recruitResult.Value);
                    }
                }
                catch (Exception)
                {
                    // Skip invalid records to prevent service-level crash
                }
            }

            return staffMembers;
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

        public async Task<Result> RestoreStaffAsync(Guid id)
        {
            return await _sender.Send(new RestoreStaffCommand(id));
        }

        public async Task<Result<List<string>>> GetAvailableFacilitiesForStaffAsync(Guid staffId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var staff = await context.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == staffId);

            if (staff == null)
            {
                return Result.Success(new List<string>());
            }

            // AllowedModules is a list of facility types (Gym, Salon, Restaurant)
            return Result.Success(staff.AllowedModules ?? new List<string>());
        }

        public async Task<Result> UpdatePermissionsAsync(Guid staffId, List<PermissionDto> permissions)
        {
            return Result.Success();
        }
    }
}
