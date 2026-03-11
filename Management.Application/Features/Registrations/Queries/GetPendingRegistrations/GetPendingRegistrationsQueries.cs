using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Registrations.Queries.GetPendingRegistrations
{
    public record GetPendingRegistrationsQuery(System.Guid FacilityId) : IRequest<Result<List<RegistrationDto>>>;
}
