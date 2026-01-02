using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Registrations.Queries.GetRegistration
{
    public record GetRegistrationQuery(Guid RegistrationId) : IRequest<Result<RegistrationDto>>;
}
