using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Registrations.Commands.SubmitRegistration
{
    public record SubmitRegistrationCommand(RegistrationDto Registration) : IRequest<Result<Guid>>;
}
