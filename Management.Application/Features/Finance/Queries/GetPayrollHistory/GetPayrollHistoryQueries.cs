using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Finance.Queries.GetPayrollHistory
{
    public record GetPayrollHistoryQuery() : IRequest<Result<List<PayrollEntryDto>>>;
}
