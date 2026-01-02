using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Staff.Commands.TerminateStaff
{
    public record TerminateStaffCommand(Guid StaffId) : IRequest<Result>;
}
