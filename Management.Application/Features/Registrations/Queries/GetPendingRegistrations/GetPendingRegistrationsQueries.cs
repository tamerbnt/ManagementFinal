using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Registrations.Queries.GetPendingRegistrations
{
    public record GetPendingRegistrationsQuery() : IRequest<Result<List<RegistrationDto>>>;
}
