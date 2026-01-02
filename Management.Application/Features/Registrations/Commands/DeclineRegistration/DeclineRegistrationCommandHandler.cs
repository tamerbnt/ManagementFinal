using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Commands.DeclineRegistration
{
    public class DeclineRegistrationCommandHandler : IRequestHandler<DeclineRegistrationCommand, Result>
    {
        private readonly IRegistrationRepository _registrationRepository;

        public DeclineRegistrationCommandHandler(IRegistrationRepository registrationRepository)
        {
            _registrationRepository = registrationRepository;
        }

        public async Task<Result> Handle(DeclineRegistrationCommand request, CancellationToken cancellationToken)
        {
            var registration = await _registrationRepository.GetByIdAsync(request.RegistrationId);
            if (registration == null)
            {
                 return Result.Failure(new Error("Registration.NotFound", "Registration not found"));
            }

            registration.Decline();
            await _registrationRepository.UpdateAsync(registration);

            return Result.Success();
        }
    }
}
