using Management.Application.Stores;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Management.Application.Features.Members.Commands.RenewMembership
{
    public class RenewMembershipCommandHandler : IRequestHandler<RenewMembershipCommand, Result>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMembershipPlanRepository _planRepository;
        private readonly IMediator _mediator;
        private readonly Microsoft.Extensions.Logging.ILogger<RenewMembershipCommandHandler> _logger;

        public RenewMembershipCommandHandler(
            IMemberRepository memberRepository,
            IMembershipPlanRepository planRepository,
            IMediator mediator,
            Microsoft.Extensions.Logging.ILogger<RenewMembershipCommandHandler> logger)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(RenewMembershipCommand request, CancellationToken cancellationToken)
        {
            foreach (var id in request.MemberIds)
            {
                var member = await _memberRepository.GetByIdAsync(id);
                if (member == null) continue; 

                int daysToAdd = 30; 
                Guid? planIdToUse = member.MembershipPlanId;

                if (planIdToUse.HasValue)
                {
                    var plan = await _planRepository.GetByIdAsync(planIdToUse.Value);
                    if (plan != null)
                    {
                        daysToAdd = plan.DurationDays;
                    }
                }

                DateTime newExpiry;
                DateTime baseline = member.ExpirationDate < DateTime.UtcNow ? DateTime.UtcNow : member.ExpirationDate;
                newExpiry = baseline.AddDays(daysToAdd);

                if (planIdToUse.HasValue)
                {
                    member.RenewReferencePlan(planIdToUse.Value, newExpiry);
                }
                
                await _memberRepository.UpdateAsync(member);

                // PUBLISH NOTIFICATION: Decoupled to prevent UI failure from stalling renewal.
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        await _mediator.Publish(new Application.Notifications.FacilityActionCompletedNotification(
                            member.FacilityId,
                            "MemberUpdate",
                            member.FullName,
                            $"Renewed membership for {member.FullName}",
                            member.Id.ToString()));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to publish renewal notification for member {MemberId}", member.Id);
                    }
                });
            }

            return Result.Success();
        }
    }
}
