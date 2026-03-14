using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Domain.Enums;
using Management.Domain.Services;
using System;

namespace Management.Application.Features.Members.Commands.UpdateMember
{
    public class UpdateMemberCommandHandler : IRequestHandler<UpdateMemberCommand, Result>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMembershipPlanRepository _planRepository;
        private readonly ISalonServiceRepository _salonRepository;
        private readonly IGymOperationService _gymService;
        private readonly IFacilityContextService _facilityContext;

        public UpdateMemberCommandHandler(
            IMemberRepository memberRepository,
            IMembershipPlanRepository planRepository,
            ISalonServiceRepository salonRepository,
            IGymOperationService gymService,
            IFacilityContextService facilityContext)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
            _salonRepository = salonRepository;
            _gymService = gymService;
            _facilityContext = facilityContext;
        }

        public async Task<Result> Handle(UpdateMemberCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Member;
            var member = await _memberRepository.GetByIdAsync(dto.Id);

            if (member == null)
            {
                return Result.Failure(new Error("Member.NotFound", $"Member with ID {dto.Id} was not found."));
            }

            var emailResult = Email.Create(dto.Email);
            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            
            if (emailResult.IsFailure) return Result.Failure(emailResult.Error);
            if (phoneResult.IsFailure) return Result.Failure(phoneResult.Error);

            member.UpdateDetails(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.CardId,
                dto.ProfileImageUrl,
                dto.Notes);

            if (!string.IsNullOrEmpty(dto.EmergencyContactName) && !string.IsNullOrEmpty(dto.EmergencyContactPhone))
            {
                 var emerPhone = PhoneNumber.Create(dto.EmergencyContactPhone);
                 if (emerPhone.IsSuccess)
                 {
                    member.UpdateEmergencyContact(
                        dto.EmergencyContactName,
                        emerPhone.Value);
                 }
            }

            // [NEW FIX] Apply Plan Updates if supplied (e.g. from Renew Flow)
            bool isPlanRenewalOrUpgrade = false;

            if (dto.MembershipPlanId.HasValue)
            {
                // Check if this constitutes a true renewal or plan change.
                if (member.MembershipPlanId != dto.MembershipPlanId.Value)
                {
                    isPlanRenewalOrUpgrade = true; // Changed plan
                }
                else if (member.ExpirationDate <= DateTime.UtcNow)
                {
                    isPlanRenewalOrUpgrade = true; // Renewing expired SAME plan
                }
                else if (dto.ExpirationDate > member.ExpirationDate)
                {
                    isPlanRenewalOrUpgrade = true; // Extending an existing plan
                }

                // We use ActivateMembership here because it sets Start, Expiration, and marks Active.
                // The QuickRegistrationViewModel passes in a precise ExpirationDate calculated based on the Plan's DurationDays.
                // We must update the Member's internal MembershipPlanId as well.
                member.RenewReferencePlan(dto.MembershipPlanId.Value, dto.ExpirationDate);
                
                // If it was previously expired, ActivateMembership resets the status to Active.
                member.ActivateMembership(dto.StartDate, dto.ExpirationDate);
            }

            await _memberRepository.UpdateAsync(member);

            // AUTO-REVENUE: Record the sale immediately if it's a renewal/upgrade.
            if (isPlanRenewalOrUpgrade && dto.MembershipPlanId.HasValue && dto.MembershipPlanId.Value != Guid.Empty)
            {
                string? planName = null;
                decimal planPrice = 0;

                if (_facilityContext.CurrentFacility == FacilityType.Salon)
                {
                    var salonService = await _salonRepository.GetByIdAsync(dto.MembershipPlanId.Value);
                    if (salonService != null)
                    {
                        planName = salonService.Name;
                        planPrice = salonService.BasePrice;
                    }
                }
                else
                {
                    var plan = await _planRepository.GetByIdAsync(dto.MembershipPlanId.Value);
                    if (plan != null)
                    {
                        planName = plan.Name;
                        planPrice = plan.Price.Amount;
                    }
                }

                if (planName != null && planPrice > 0)
                {
                    // Create Sale logic mirroring CreateMemberCommandHandler
                    await _gymService.SellItemAsync(
                        member.Id.ToString(),
                        planPrice,
                        planName,                              // Product Name
                        _facilityContext.CurrentFacilityId,    // Facility Id (scoped)
                        planName,                             // Transaction Type
                        _facilityContext.CurrentFacility == FacilityType.Salon ? SaleCategory.Service : SaleCategory.Membership,
                        planName                              // Captured Label
                    );
                }
            }

            return Result.Success();
        }
    }
}
