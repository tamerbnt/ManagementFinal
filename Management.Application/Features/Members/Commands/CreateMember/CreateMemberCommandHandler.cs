using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Domain.Services;
using Management.Domain.Enums;

namespace Management.Application.Features.Members.Commands.CreateMember
{
    public class CreateMemberCommandHandler : IRequestHandler<CreateMemberCommand, Result<Guid>>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly Domain.Services.ITenantService _tenantService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMembershipPlanRepository _planRepository;
        private readonly ISalonServiceRepository _salonRepository;
        private readonly IGymOperationService _gymService;
        private readonly IFacilityContextService _facilityContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;

        public CreateMemberCommandHandler(
            IMemberRepository memberRepository, 
            Domain.Services.ITenantService tenantService,
            ICurrentUserService currentUserService,
            IMembershipPlanRepository planRepository,
            ISalonServiceRepository salonRepository,
            IGymOperationService gymService,
            IFacilityContextService facilityContext,
            IUnitOfWork unitOfWork,
            IMediator mediator)
        {
            _memberRepository = memberRepository;
            _tenantService = tenantService;
            _currentUserService = currentUserService;
            _planRepository = planRepository;
            _salonRepository = salonRepository;
            _gymService = gymService;
            _facilityContext = facilityContext;
            _unitOfWork = unitOfWork;
            _mediator = mediator;
        }

        public async Task<Result<Guid>> Handle(CreateMemberCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Member;

            var emailResult = Email.Create(dto.Email);
            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            
            if (emailResult.IsFailure) return Result.Failure<Guid>(emailResult.Error);
            if (phoneResult.IsFailure) return Result.Failure<Guid>(phoneResult.Error);

            var result = Member.Register(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.CardId,
                dto.MembershipPlanId);

            if (result.IsFailure)
            {
                return Result.Failure<Guid>(result.Error);
            }

            var member = result.Value;

            // Resolve Expiration duration from plan or default
            int durationDays = 30;
            string? planName = null;
            decimal planPrice = 0;

            if (dto.MembershipPlanId.HasValue && dto.MembershipPlanId.Value != Guid.Empty)
            {
                if (_facilityContext.CurrentFacility == FacilityType.Salon)
                {
                    var salonService = await _salonRepository.GetByIdAsync(dto.MembershipPlanId.Value);
                    if (salonService != null)
                    {
                        durationDays = 365; // Salon "memberships" default to 1 year
                        planName = salonService.Name;
                        planPrice = salonService.BasePrice;
                    }
                }
                else
                {
                    var plan = await _planRepository.GetByIdAsync(dto.MembershipPlanId.Value);
                    if (plan != null)
                    {
                        durationDays = plan.DurationDays;
                        planName = plan.Name;
                        planPrice = plan.Price.Amount;
                    }
                }
            }

            if (dto.Status == MemberStatus.Active)
            {
                var startDate = dto.StartDate != default ? dto.StartDate : DateTime.UtcNow;
                var expirationDate = dto.ExpirationDate != default ? dto.ExpirationDate : startDate.AddDays(durationDays);
                member.ActivateMembership(startDate, expirationDate);
            }
            
            var tenantId = _tenantService.GetTenantId();
            if (tenantId.HasValue)
            {
                member.TenantId = tenantId.Value;
            }

            var facilityId = _facilityContext.CurrentFacilityId;
            if (facilityId != Guid.Empty)
            {
                member.FacilityId = facilityId;
            }

            if (!string.IsNullOrEmpty(dto.Notes)) 
            {
                member.UpdateDetails(
                    member.FullName, 
                    member.Email, 
                    member.PhoneNumber, 
                    member.CardId, 
                    dto.ProfileImageUrl, 
                    dto.Notes);
            }

            if (!string.IsNullOrEmpty(dto.EmergencyContactName) && !string.IsNullOrEmpty(dto.EmergencyContactPhone))
            {
                var emerPhone = PhoneNumber.Create(dto.EmergencyContactPhone);
                if (emerPhone.IsSuccess)
                {
                    member.UpdateEmergencyContact(dto.EmergencyContactName, emerPhone.Value);
                }
            }
            
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _memberRepository.AddAsync(member, saveChanges: false);

                // PUBLISH NOTIFICATION: This is critical for the "Active Members" and "Pending Registrations" cards.
                // Even if no sale occurs, the UI needs to know a registration was completed.
                await _mediator.Publish(new Application.Notifications.FacilityActionCompletedNotification(
                    member.FacilityId,
                    "Registration",
                    member.FullName,
                    "New Member Registered",
                    member.Id.ToString()), cancellationToken);

                // AUTO-REVENUE: If a plan was selected, record the sale immediately.
                if (planName != null && planPrice > 0)
                {
                    // Create Sale
                    // Note: We use the plan name for both TransactionType and CapturedLabel for clarity
                    var saleSuccess = await _gymService.SellItemAsync(
                        member.Id.ToString(),
                        planPrice,
                        planName,                              // Product Name
                        member.FacilityId,                     // Facility Id (scoped)
                        planName,                             // Transaction Type
                        _facilityContext.CurrentFacility == FacilityType.Salon ? SaleCategory.Service : SaleCategory.Membership,
                        planName                              // Captured Label
                    );

                    if (!saleSuccess)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result.Failure<Guid>(new Error("Member.SaleFailed", "Failed to record membership sale."));
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(member.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Guid>(new Error("Member.DatabaseError", $"Failed to save member: {ex.Message}"));
            }
        }
    }
}
