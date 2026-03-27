using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Finance.Commands.CreatePayrollEntry
{
    public record CreatePayrollEntryCommand(PayrollEntryDto Entry, bool IsPaid = false) : IRequest<Result<Guid>>;
}
