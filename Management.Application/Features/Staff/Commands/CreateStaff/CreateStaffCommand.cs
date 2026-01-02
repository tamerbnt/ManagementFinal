using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Staff.Commands.CreateStaff
{
    public record CreateStaffCommand(StaffDto Staff) : IRequest<Result<Guid>>;
}
