using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Management.Application.Features.Staff.Commands.UpdateStaff
{
    public class UpdateStaffCommandHandler : IRequestHandler<UpdateStaffCommand, Result<Guid>>
    {
        private readonly IStaffRepository _staffRepository;

        public UpdateStaffCommandHandler(IStaffRepository staffRepository)
        {
            _staffRepository = staffRepository;
        }

        public async Task<Result<Guid>> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Staff;
            var staff = await _staffRepository.GetByIdAsync(dto.Id);

            if (staff == null)
            {
                return Result.Failure<Guid>(new Error("Staff.NotFound", $"Staff with ID {dto.Id} was not found."));
            }

            var emailResult = Email.Create(dto.Email);
            if (emailResult.IsFailure) return Result.Failure<Guid>(emailResult.Error);

            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            if (phoneResult.IsFailure) return Result.Failure<Guid>(phoneResult.Error);

            staff.UpdateDetails(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.Role,
                dto.Salary,
                dto.PaymentDay);

            if (dto.Permissions != null)
            {
                foreach (var p in dto.Permissions)
                {
                    staff.SetPermission(p.Name, p.IsGranted);
                }
            }

            // Fix 5: Hash PIN if provided
            if (!string.IsNullOrEmpty(dto.Password))
            {
                staff.SetPinCode(BCryptNet.HashPassword(dto.Password));
            }

            await _staffRepository.UpdateAsync(staff);

            return Result.Success(staff.Id);
        }
    }
}
