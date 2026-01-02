using Management.Domain.DTOs;
using Management.Domain.Enums;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Registrations.Queries.SearchRegistrations
{
    public record SearchRegistrationsQuery(string SearchText, RegistrationFilterType Filter) : IRequest<List<RegistrationDto>>;
}
