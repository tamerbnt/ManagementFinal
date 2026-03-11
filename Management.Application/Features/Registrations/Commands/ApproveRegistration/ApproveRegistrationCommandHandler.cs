using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Commands.ApproveRegistration
{
    public class ApproveRegistrationCommandHandler : IRequestHandler<ApproveRegistrationCommand, Result<Guid>>
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IMemberRepository _memberRepository;

        public ApproveRegistrationCommandHandler(
            IRegistrationRepository registrationRepository,
            IMemberRepository memberRepository)
        {
            _registrationRepository = registrationRepository;
            _memberRepository = memberRepository;
        }

        public async Task<Result<Guid>> Handle(ApproveRegistrationCommand request, CancellationToken cancellationToken)
        {
            var registration = await _registrationRepository.GetByIdAsync(request.RegistrationId, request.FacilityId);
            if (registration == null)
            {
                return Result.Failure<Guid>(new Error("Registration.NotFound", "Registration not found"));
            }

            if (registration.Status != Domain.Enums.RegistrationStatus.Pending)
            {
                return Result.Failure<Guid>(new Error("Registration.NotPending", "Only pending registrations can be approved"));
            }

            // Approve Registration
            registration.Approve();
            await _registrationRepository.UpdateAsync(registration);

            // Create Member
            var memberResult = Member.Register(
                registration.FullName,
                registration.Email,
                registration.PhoneNumber,
                Guid.NewGuid().ToString().Substring(0, 8).ToUpper(), // Generate temp CardId
                registration.PreferredPlanId);

            if (memberResult.IsFailure) return Result.Failure<Guid>(memberResult.Error);

            var member = memberResult.Value;
            member.FacilityId = request.FacilityId;
            await _memberRepository.AddAsync(member);

            return Result.Success(member.Id);
        }
    }
}
