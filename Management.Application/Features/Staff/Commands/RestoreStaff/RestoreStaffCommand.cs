using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Staff.Commands.RestoreStaff
{
    public record RestoreStaffCommand(Guid StaffId) : IRequest<Result>;
}
