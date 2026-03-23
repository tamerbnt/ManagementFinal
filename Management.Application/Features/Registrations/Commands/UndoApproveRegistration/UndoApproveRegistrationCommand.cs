using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Registrations.Commands.UndoApproveRegistration
{
    public record UndoApproveRegistrationCommand(Guid RegistrationId, Guid FacilityId) : IRequest<Result>;
}
