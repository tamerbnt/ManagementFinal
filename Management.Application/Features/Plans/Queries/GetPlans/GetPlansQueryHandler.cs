using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Plans.Queries.GetPlans
{
    public class GetPlansQueryHandler : IRequestHandler<GetPlansQuery, Result<List<MembershipPlanDto>>>
    {
        private readonly IMembershipPlanRepository _planRepository;

        public GetPlansQueryHandler(IMembershipPlanRepository planRepository)
        {
            _planRepository = planRepository;
        }

        public async Task<Result<List<MembershipPlanDto>>> Handle(GetPlansQuery request, CancellationToken cancellationToken)
        {
            // The handler already passes the ActiveOnly flag to the repository.
            // The actual logic for respecting ActiveOnly and handling IsDeleted when ignoring filters
            // should be implemented within the GetActivePlansAsync method of the IMembershipPlanRepository implementation.
            var plans = await _planRepository.GetActivePlansAsync(request.FacilityId, request.ActiveOnly);

            var dtos = plans.Select(p => new MembershipPlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                DurationDays = p.DurationDays,
                Price = p.Price.Amount,
                IsActive = p.IsActive,
                IsSessionPack = p.IsSessionPack,
                IsWalkIn = p.IsWalkIn,
                GenderRule = p.GenderRule,
                ScheduleJson = p.ScheduleJson
            }).ToList();

            return Result.Success(dtos);
        }
    }
}
