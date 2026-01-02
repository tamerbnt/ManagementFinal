using Management.Domain.DTOs;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.History.Queries.GetUnifiedHistory
{
    public record GetUnifiedHistoryQuery(DateTime StartDate, DateTime EndDate) : IRequest<IEnumerable<UnifiedHistoryEventDto>>;
}
