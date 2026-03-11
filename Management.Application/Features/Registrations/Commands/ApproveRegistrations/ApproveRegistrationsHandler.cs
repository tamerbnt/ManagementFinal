using Management.Application.Features.Registrations.Commands.ApproveRegistrations;
using Management.Application.Services;
using Management.Domain.Services;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Commands.ApproveRegistrations
{
    public class ApproveRegistrationsHandler : IRequestHandler<ApproveRegistrationsCommand, bool>
    {
        private readonly IRegistrationService _registrationService;
        private readonly IFacilityContextService _facilityContext;

        public ApproveRegistrationsHandler(IRegistrationService registrationService, IFacilityContextService facilityContext)
        {
            _registrationService = registrationService;
            _facilityContext = facilityContext;
        }

        public async Task<bool> Handle(ApproveRegistrationsCommand request, CancellationToken cancellationToken)
        {
            if (request.RegistrationIds == null || !request.RegistrationIds.Any())
                return false;

            var facilityId = _facilityContext.CurrentFacilityId;

            if (request.RegistrationIds.Count == 1)
            {
                await _registrationService.ApproveRegistrationAsync(request.RegistrationIds.First(), facilityId);
            }
            else
            {
                await _registrationService.ApproveBatchAsync(request.RegistrationIds, facilityId);
            }

            return true;
        }
    }
}
