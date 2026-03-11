using System.Collections.Generic;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using MediatR;

namespace Management.Application.Features.Registrations.Queries.SearchRegistrations
{
    public record SearchRegistrationsQuery(
        string SearchText, 
        RegistrationFilterType Filter, 
        RegistrationStatus? Status = null,
        int Page = 1, 
        int PageSize = 20) : IRequest<Result<PagedResult<RegistrationDto>>>;
}
