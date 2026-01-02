using Management.Application.Stores;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Commands.RenewMembership
{
    public class RenewMembershipCommandHandler : IRequestHandler<RenewMembershipCommand, Result>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMembershipPlanRepository _planRepository;

        public RenewMembershipCommandHandler(
            IMemberRepository memberRepository,
            IMembershipPlanRepository planRepository)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
        }

        public async Task<Result> Handle(RenewMembershipCommand request, CancellationToken cancellationToken)
        {
            foreach (var id in request.MemberIds)
            {
                var member = await _memberRepository.GetByIdAsync(id);
                if (member == null) continue; 

                int monthsToAdd = 1; 
                Guid? planIdToUse = member.MembershipPlanId;

                if (planIdToUse.HasValue)
                {
                    var plan = await _planRepository.GetByIdAsync(planIdToUse.Value);
                    if (plan != null)
                    {
                        monthsToAdd = plan.DurationDays / 30; 
                        if (monthsToAdd < 1) monthsToAdd = 1;
                    }
                }

                DateTime newExpiry;
                if (member.ExpirationDate < DateTime.UtcNow)
                {
                    newExpiry = DateTime.UtcNow.AddMonths(monthsToAdd);
                }
                else
                {
                    newExpiry = member.ExpirationDate.AddMonths(monthsToAdd);
                }

                if (planIdToUse.HasValue)
                {
                    member.RenewReferencePlan(planIdToUse.Value, newExpiry);
                }
                
                await _memberRepository.UpdateAsync(member);
            }

            return Result.Success();
        }
    }
}
