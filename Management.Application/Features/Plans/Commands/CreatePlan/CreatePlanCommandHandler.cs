using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Plans.Commands.CreatePlan
{
    public class CreatePlanCommandHandler : IRequestHandler<CreatePlanCommand, Result<Guid>>
    {
        private readonly IMembershipPlanRepository _planRepository;

        public CreatePlanCommandHandler(IMembershipPlanRepository planRepository)
        {
            _planRepository = planRepository;
        }

        public async Task<Result<Guid>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Plan;

            var price = new Money(dto.Price, "USD");
            
            var planResult = MembershipPlan.Create(
                dto.Name,
                dto.Description,
                dto.DurationDays,
                price);

            if (planResult.IsFailure) return Result.Failure<Guid>(planResult.Error);

            var plan = planResult.Value;
            await _planRepository.AddAsync(plan);

            return Result.Success(plan.Id);
        }
    }
}
