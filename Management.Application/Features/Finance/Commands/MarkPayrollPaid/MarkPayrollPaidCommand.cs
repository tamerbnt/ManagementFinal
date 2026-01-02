using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Finance.Commands.MarkPayrollPaid
{
    public record MarkPayrollPaidCommand(Guid EntryId) : IRequest<Result>;
}
