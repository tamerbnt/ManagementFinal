using Management.Application.Features.Plans.Queries.GetPlans;
using Management.Application.Services;
using Management.Application.Features.Plans.Commands.CreatePlan;
using Management.Application.Services;
using Management.Application.Features.Plans.Commands.UpdatePlan;
using Management.Application.Services;
using Management.Application.Features.Plans.Commands.DeletePlan;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Infrastructure.Services
{
    public class MembershipPlanService : IMembershipPlanService
    {
        private readonly ISender _sender;

        public MembershipPlanService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result<List<MembershipPlanDto>>> GetAllPlansAsync()
        {
            return await _sender.Send(new GetPlansQuery(ActiveOnly: false));
        }

        public async Task<Result> CreatePlanAsync(MembershipPlanDto plan)
        {
            return await _sender.Send(new CreatePlanCommand(plan));
        }

        public async Task<Result> UpdatePlanAsync(MembershipPlanDto plan)
        {
            return await _sender.Send(new UpdatePlanCommand(plan));
        }

        public async Task<Result> DeletePlanAsync(Guid id)
        {
            return await _sender.Send(new DeletePlanCommand(id));
        }
    }
}
