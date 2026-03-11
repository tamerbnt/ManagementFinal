using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Staff.Queries.GetStaff
{
    public class GetStaffQueryHandler : 
        IRequestHandler<GetStaffQuery, Result<StaffDto>>,
        IRequestHandler<GetAllStaffQuery, Result<List<StaffDto>>>
    {
        private readonly IStaffRepository _staffRepository;

        public GetStaffQueryHandler(IStaffRepository staffRepository)
        {
            _staffRepository = staffRepository;
        }

        public async Task<Result<StaffDto>> Handle(GetStaffQuery request, CancellationToken cancellationToken)
        {
            var staff = await _staffRepository.GetByIdAsync(request.Id);
            if (staff == null)
            {
                return Result.Failure<StaffDto>(new Error("Staff.NotFound", "Staff member not found"));
            }

            return Result.Success(MapToDto(staff));
        }

        public async Task<Result<List<StaffDto>>> Handle(GetAllStaffQuery request, CancellationToken cancellationToken)
        {
            var allStaff = await _staffRepository.GetAllAsync();
            var dtos = allStaff.Take(200).Select(MapToDto).ToList();
            return Result.Success(dtos);
        }

        private static StaffDto MapToDto(StaffMember entity)
        {
            return new StaffDto
            {
                Id = entity.Id,
                TenantId = entity.TenantId,
                FacilityId = entity.FacilityId,
                FullName = entity.FullName,
                Email = entity.Email.Value,
                PhoneNumber = entity.PhoneNumber.Value,
                Role = entity.Role,
                HireDate = entity.HireDate, 
                Salary = entity.Salary,
                PaymentDay = entity.PaymentDay,
                Status = entity.IsActive ? "Active" : "Inactive",
                Permissions = entity.Permissions?.Select(p => new PermissionDto(p.Key, p.Value)).ToList() ?? new List<PermissionDto>()
            };
        }
    }
}
