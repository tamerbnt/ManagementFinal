using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Facility.Queries
{
    public record GetZonesQuery() : IRequest<Result<List<ZoneDto>>>;
    public record GetIntegrationsQuery() : IRequest<Result<List<IntegrationDto>>>;
}
