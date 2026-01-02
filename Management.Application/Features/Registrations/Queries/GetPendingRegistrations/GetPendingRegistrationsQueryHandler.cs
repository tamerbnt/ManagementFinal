using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Queries.GetPendingRegistrations
{
    public class GetPendingRegistrationsQueryHandler : IRequestHandler<GetPendingRegistrationsQuery, Result<List<RegistrationDto>>>
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IMembershipPlanRepository _planRepository;

        public GetPendingRegistrationsQueryHandler(IRegistrationRepository registrationRepository, IMembershipPlanRepository planRepository)
        {
            _registrationRepository = registrationRepository;
            _planRepository = planRepository;
        }

        public async Task<Result<List<RegistrationDto>>> Handle(GetPendingRegistrationsQuery request, CancellationToken cancellationToken)
        {
            var pending = await _registrationRepository.GetPendingRegistrationsAsync();
            var dtos = new List<RegistrationDto>();

            foreach (var reg in pending)
            {
                dtos.Add(await MapToDto(reg));
            }

            return Result.Success(dtos);
        }

        private async Task<RegistrationDto> MapToDto(Registration entity)
        {
            string planName = "Unknown";
            if (entity.PreferredPlanId.HasValue)
            {
                var plan = await _planRepository.GetByIdAsync(entity.PreferredPlanId.Value);
                if (plan != null) planName = plan.Name;
            }

            return new RegistrationDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email.Value,
                PhoneNumber = entity.PhoneNumber.Value,
                Source = entity.Source,
                CreatedAt = entity.CreatedAt,
                Status = entity.Status,
                PreferredPlanName = planName,
                PreferredPlanId = entity.PreferredPlanId,
                PreferredStartDate = entity.PreferredStartDate,
                Notes = entity.Notes
            };
        }
    }
}
