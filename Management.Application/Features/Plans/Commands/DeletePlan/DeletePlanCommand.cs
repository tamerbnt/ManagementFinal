using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Plans.Commands.DeletePlan
{
    public record DeletePlanCommand(Guid PlanId, Guid? FacilityId = null) : IRequest<Result>;
}
