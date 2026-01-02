using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Commands.SubmitRegistration
{
    public class SubmitRegistrationCommandHandler : IRequestHandler<SubmitRegistrationCommand, Result<Guid>>
    {
        private readonly IRegistrationRepository _registrationRepository;

        public SubmitRegistrationCommandHandler(IRegistrationRepository registrationRepository)
        {
            _registrationRepository = registrationRepository;
        }

        public async Task<Result<Guid>> Handle(SubmitRegistrationCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Registration;

            var emailResult = Email.Create(dto.Email);
            if (emailResult.IsFailure) return Result.Failure<Guid>(emailResult.Error);

            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            if (phoneResult.IsFailure) return Result.Failure<Guid>(phoneResult.Error);

            var registrationResult = Registration.Submit(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.Source,
                dto.PreferredPlanId,
                dto.PreferredStartDate,
                dto.Notes);

            if (registrationResult.IsFailure) return Result.Failure<Guid>(registrationResult.Error);

            var registration = registrationResult.Value;
            await _registrationRepository.AddAsync(registration);

            return Result.Success(registration.Id);
        }
    }
}
