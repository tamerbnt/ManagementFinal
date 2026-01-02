using Management.Application.Features.Registrations.Queries.SearchRegistrations;
using Management.Domain.DTOs;
using Management.Domain.Services;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
