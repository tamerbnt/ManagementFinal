using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Plans.Queries.GetPlans
{
    public record GetPlansQuery(bool ActiveOnly = true) : IRequest<Result<List<MembershipPlanDto>>>;
}
