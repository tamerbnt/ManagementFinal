using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Turnstiles.Queries
{
    public record GetTurnstilesQuery() : IRequest<Result<List<TurnstileDto>>>;
    public record GetAccessEventsQuery(Guid? TurnstileId = null, DateTime? FromDate = null) : IRequest<Result<List<AccessEventDto>>>;
    public record GetCurrentOccupancyQuery() : IRequest<Result<int>>;
}
