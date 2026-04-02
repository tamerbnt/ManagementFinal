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
        private readonly Domain.Services.ITenantService _tenantService;
        private readonly Interfaces.ICurrentUserService _currentUserService;

        public CreatePlanCommandHandler(
            IMembershipPlanRepository planRepository,
            Domain.Services.ITenantService tenantService,
            Interfaces.ICurrentUserService currentUserService)
        {
            _planRepository = planRepository;
            _tenantService = tenantService;
            _currentUserService = currentUserService;
        }

        public async Task<Result<Guid>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Plan;

            var price = new Money(dto.Price, "DA");
            
            var planResult = MembershipPlan.Create(
                dto.Name,
                dto.Description,
                dto.DurationDays,
                price,
                0, // BaseSessionCount
                dto.IsWalkIn);

            if (planResult.IsFailure) return Result.Failure<Guid>(planResult.Error);

            var plan = planResult.Value;
            plan.GenderRule = dto.GenderRule;
            plan.ScheduleJson = dto.ScheduleJson;
            plan.IsSessionPack = dto.IsSessionPack;

            // Set multi-tenancy IDs
            var tenantId = _tenantService.GetTenantId();
            if (tenantId.HasValue) plan.TenantId = tenantId.Value;

            var facilityId = request.FacilityId ?? _currentUserService.CurrentFacilityId;
            if (facilityId.HasValue && facilityId != Guid.Empty) plan.FacilityId = facilityId.Value;

            await _planRepository.AddAsync(plan);

            return Result.Success(plan.Id);
        }
    }
}
