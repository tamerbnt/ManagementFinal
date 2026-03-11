using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Plans.Commands.CreatePlan
{
    public record CreatePlanCommand(MembershipPlanDto Plan, Guid? FacilityId = null) : IRequest<Result<Guid>>;
}
