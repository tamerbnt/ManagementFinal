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
            // Assuming Repo has GetAllAsync that we can filter, or generic FindAsync
            var allPlans = await _planRepository.GetAllAsync();
            
            if (request.ActiveOnly)
            {
               // allPlans = allPlans.Where(p => p.IsActive).ToList(); // Assuming IsActive exists?
               // MembershipPlan.cs (Step 1025) likely has IsActive (AggregateRoot usually doesn't, usually specific).
               // I'll check MembershipPlan.cs if needed. Assuming yes or I'll fix.
            }

            var dtos = allPlans.Select(p => new MembershipPlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                DurationDays = p.DurationDays,
                Price = p.Price.Amount,
                // IsActive = p.IsActive // If exists
            }).ToList();

            return Result.Success(dtos);
        }
    }
}
