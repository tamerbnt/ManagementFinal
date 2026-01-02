using Management.Application.Features.Registrations.Queries.SearchRegistrations;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Application.Features.Registrations.Queries.SearchRegistrations
{
    public class SearchRegistrationsHandler : IRequestHandler<SearchRegistrationsQuery, List<RegistrationDto>>
    {
        private readonly IRegistrationService _registrationService;

        public SearchRegistrationsHandler(IRegistrationService registrationService)
        {
            _registrationService = registrationService;
        }

        public async Task<List<RegistrationDto>> Handle(SearchRegistrationsQuery request, CancellationToken cancellationToken)
        {
            var result = await _registrationService.SearchAsync(new RegistrationSearchRequest(request.SearchText, request.Filter));
            
            if (result.IsSuccess)
            {
                return result.Value.Items.ToList();
            }

            return new List<RegistrationDto>();
        }
    }
}
