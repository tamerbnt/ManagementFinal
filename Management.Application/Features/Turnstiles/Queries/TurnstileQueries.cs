using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Turnstiles.Queries
{
    public record GetTurnstilesQuery(Guid FacilityId) : IRequest<Result<List<TurnstileDto>>>;
    public record GetAccessEventsQuery(Guid FacilityId, Guid? TurnstileId = null, DateTime? FromDate = null) : IRequest<Result<List<AccessEventDto>>>;
    public record GetCurrentOccupancyQuery(Guid FacilityId) : IRequest<Result<int>>;
}
