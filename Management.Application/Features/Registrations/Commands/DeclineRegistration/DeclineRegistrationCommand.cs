using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Registrations.Commands.DeclineRegistration
{
    public record DeclineRegistrationCommand(Guid RegistrationId, Guid FacilityId) : IRequest<Result>;
}
