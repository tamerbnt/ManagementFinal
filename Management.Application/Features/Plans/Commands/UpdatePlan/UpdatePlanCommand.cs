using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Plans.Commands.UpdatePlan
{
    public record UpdatePlanCommand(MembershipPlanDto Plan) : IRequest<Result<Guid>>;
}
