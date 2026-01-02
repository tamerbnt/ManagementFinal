using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Staff.Commands.UpdateStaff
{
    public record UpdateStaffCommand(StaffDto Staff) : IRequest<Result<Guid>>;
}
