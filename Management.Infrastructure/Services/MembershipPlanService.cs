using Management.Application.Features.Plans.Queries.GetPlans;
using Management.Application.Services;
using Management.Application.Features.Plans.Commands.CreatePlan;
using Management.Application.Features.Plans.Commands.UpdatePlan;
using Management.Application.Features.Plans.Commands.DeletePlan;
using Management.Application.Features.Plans.Commands.RestorePlan;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class MembershipPlanService : IMembershipPlanService
    {
        private readonly ISender _sender;
        private readonly IAccessControlCache _cache;

        public MembershipPlanService(ISender sender, IAccessControlCache cache)
        {
            _sender = sender;
            _cache = cache;
        }

        public async Task<Result<List<MembershipPlanDto>>> GetAllPlansAsync(Guid facilityId)
        {
            return await _sender.Send(new GetPlansQuery(ActiveOnly: false, FacilityId: facilityId));
        }

        public async Task<Result> CreatePlanAsync(Guid facilityId, MembershipPlanDto plan)
        {
            return await _sender.Send(new CreatePlanCommand(plan, facilityId));
        }

        public async Task<Result> UpdatePlanAsync(Guid facilityId, MembershipPlanDto plan)
        {
            var result = await _sender.Send(new UpdatePlanCommand(plan, facilityId));
            if (result.IsSuccess && plan.Id != Guid.Empty)
            {
                _cache.InvalidatePlanSchedule(plan.Id);
            }
            return result;
        }

        public async Task<Result> DeletePlanAsync(Guid facilityId, Guid id)
        {
            var result = await _sender.Send(new DeletePlanCommand(id, facilityId));
            if (result.IsSuccess)
            {
                _cache.InvalidatePlanSchedule(id);
            }
            return result;
        }

        public async Task<Result> RestorePlanAsync(Guid facilityId, Guid id)
        {
            var result = await _sender.Send(new RestorePlanCommand(id, facilityId));
            if (result.IsSuccess)
            {
                _cache.InvalidatePlanSchedule(id);
            }
            return result;
        }
    }
}
