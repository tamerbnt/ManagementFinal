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
            var pending = (await _registrationRepository.GetPendingRegistrationsAsync(request.FacilityId)).ToList();
            
            // Fix N+1: Fetch all plans once for mapping
            var allPlans = await _planRepository.GetActivePlansAsync(request.FacilityId);
            var plans = allPlans.ToDictionary(p => p.Id, p => p.Name);

            var dtos = pending.Select(reg => new RegistrationDto
            {
                Id = reg.Id,
                FullName = reg.FullName,
                Email = reg.Email.Value,
                PhoneNumber = reg.PhoneNumber.Value,
                Source = reg.Source,
                CreatedAt = reg.CreatedAt,
                Status = reg.Status,
                PreferredPlanName = reg.PreferredPlanId.HasValue && plans.TryGetValue(reg.PreferredPlanId.Value, out var name) ? name : "Unknown",
                PreferredPlanId = reg.PreferredPlanId,
                PreferredStartDate = reg.PreferredStartDate,
                Notes = reg.Notes
            }).ToList();

            return Result.Success(dtos);
        }
    }
}
