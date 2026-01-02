using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Facility.Commands.RegisterZone
{
    public record RegisterZoneCommand(ZoneDto Zone) : IRequest<Result<Guid>>;
}
