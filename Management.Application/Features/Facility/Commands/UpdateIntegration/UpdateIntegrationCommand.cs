using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Facility.Commands.UpdateIntegration
{
    public record UpdateIntegrationCommand(IntegrationDto Integration) : IRequest<Result<Guid>>;
}
