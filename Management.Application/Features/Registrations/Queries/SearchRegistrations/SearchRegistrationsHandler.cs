using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;

namespace Management.Application.Features.Registrations.Queries.SearchRegistrations
{
    public class SearchRegistrationsHandler : IRequestHandler<SearchRegistrationsQuery, Result<PagedResult<RegistrationDto>>>
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IFacilityContextService _facilityContext;

        public SearchRegistrationsHandler(IRegistrationRepository registrationRepository, IFacilityContextService facilityContext)
        {
            _registrationRepository = registrationRepository;
            _facilityContext = facilityContext;
        }

        public async Task<Result<PagedResult<RegistrationDto>>> Handle(SearchRegistrationsQuery request, CancellationToken cancellationToken)
        {
            var facilityId = _facilityContext.CurrentFacilityId == Guid.Empty ? (Guid?)null : _facilityContext.CurrentFacilityId;
            
            var (items, totalCount) = await _registrationRepository.SearchPagedAsync(
                request.SearchText,
                facilityId,
                request.Page,
                request.PageSize,
                request.Status,
                request.Filter);

            var dtos = items.Select(r => new RegistrationDto
            {
                Id = r.Id,
                FullName = r.FullName,
                Email = r.Email.Value,
                PhoneNumber = r.PhoneNumber.Value,
                Source = r.Source,
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                PreferredPlanId = r.PreferredPlanId,
                PreferredPlanName = "Plan", // Simplified for now, similar to Member search optimization
                Notes = r.Notes
            }).ToList();

            return Result.Success(new PagedResult<RegistrationDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = request.Page,
                PageSize = request.PageSize
            });
        }
    }
}
