using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Staff.Commands.CreateStaff
{
    public class CreateStaffCommandHandler : IRequestHandler<CreateStaffCommand, Result<Guid>>
    {
        private readonly IStaffRepository _staffRepository;

        public CreateStaffCommandHandler(IStaffRepository staffRepository)
        {
            _staffRepository = staffRepository;
        }

        public async Task<Result<Guid>> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Staff;

            var emailResult = Email.Create(dto.Email);
            if (emailResult.IsFailure) return Result.Failure<Guid>(emailResult.Error);

            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            if (phoneResult.IsFailure) return Result.Failure<Guid>(phoneResult.Error);

            var staffResult = StaffMember.Recruit(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.Role);

            if (staffResult.IsFailure) return Result.Failure<Guid>(staffResult.Error);

            var staff = staffResult.Value;
            await _staffRepository.AddAsync(staff);

            return Result.Success(staff.Id);
        }
    }
}
