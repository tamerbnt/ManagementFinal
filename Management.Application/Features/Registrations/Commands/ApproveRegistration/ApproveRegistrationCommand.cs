using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Registrations.Commands.ApproveRegistration
{
    public record ApproveRegistrationCommand(Guid RegistrationId) : IRequest<Result<Guid>>; // Returns MemberId
}
