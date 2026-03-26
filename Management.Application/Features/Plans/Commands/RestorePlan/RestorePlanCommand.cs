using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Plans.Commands.RestorePlan
{
    public record RestorePlanCommand(Guid PlanId, Guid? FacilityId = null) : IRequest<Result>;
}
