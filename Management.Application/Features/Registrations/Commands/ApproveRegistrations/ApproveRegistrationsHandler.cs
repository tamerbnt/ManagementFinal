using Management.Application.Features.Registrations.Commands.ApproveRegistrations;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Threading;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Application.Features.Registrations.Commands.ApproveRegistrations
{
    public class ApproveRegistrationsHandler : IRequestHandler<ApproveRegistrationsCommand, bool>
    {
        private readonly IRegistrationService _registrationService;

        public ApproveRegistrationsHandler(IRegistrationService registrationService)
        {
            _registrationService = registrationService;
        }

        public async Task<bool> Handle(ApproveRegistrationsCommand request, CancellationToken cancellationToken)
        {
            if (request.RegistrationIds == null || !request.RegistrationIds.Any())
                return false;

            if (request.RegistrationIds.Count == 1)
            {
                await _registrationService.ApproveRegistrationAsync(request.RegistrationIds.First());
            }
            else
            {
                await _registrationService.ApproveBatchAsync(request.RegistrationIds);
            }

            return true;
        }
    }
}
