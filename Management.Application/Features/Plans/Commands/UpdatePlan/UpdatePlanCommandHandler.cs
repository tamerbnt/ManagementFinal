using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Plans.Commands.UpdatePlan
{
    public class UpdatePlanCommandHandler : IRequestHandler<UpdatePlanCommand, Result<Guid>>
    {
        private readonly IMembershipPlanRepository _planRepository;

        public UpdatePlanCommandHandler(IMembershipPlanRepository planRepository)
        {
            _planRepository = planRepository;
        }

        public async Task<Result<Guid>> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Plan;
            var plan = await _planRepository.GetByIdAsync(dto.Id);

            if (plan == null)
            {
                 return Result.Failure<Guid>(new Error("Plan.NotFound", $"Plan with ID {dto.Id} not found"));
            }

            var price = new Money(dto.Price, "USD");

            plan.UpdateDetails(
                dto.Name,
                dto.Description,
                dto.DurationDays,
                price);

            if (dto.IsActive) plan.Activate(); else plan.Deactivate();

            await _planRepository.UpdateAsync(plan);

            return Result.Success(plan.Id);
        }
    }
}
