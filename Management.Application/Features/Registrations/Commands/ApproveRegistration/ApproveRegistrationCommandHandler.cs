using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Commands.ApproveRegistration
{
    public class ApproveRegistrationCommandHandler : IRequestHandler<ApproveRegistrationCommand, Result<(Guid MemberId, Guid? SaleId)>>
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IMemberRepository _memberRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IMembershipPlanRepository _membershipPlanRepository;
        private readonly Management.Domain.Services.ITenantService _tenantService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ApproveRegistrationCommandHandler> _logger;

        public ApproveRegistrationCommandHandler(
            IRegistrationRepository registrationRepository,
            IMemberRepository memberRepository,
            ISaleRepository saleRepository,
            IMembershipPlanRepository membershipPlanRepository,
            Management.Domain.Services.ITenantService tenantService,
            IUnitOfWork unitOfWork,
            ILogger<ApproveRegistrationCommandHandler> logger)
        {
            _registrationRepository = registrationRepository;
            _memberRepository = memberRepository;
            _saleRepository = saleRepository;
            _membershipPlanRepository = membershipPlanRepository;
            _tenantService = tenantService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<(Guid MemberId, Guid? SaleId)>> Handle(ApproveRegistrationCommand request, CancellationToken cancellationToken)
        {
            var registration = await _registrationRepository.GetByIdAsync(request.RegistrationId, request.FacilityId);
            if (registration == null)
            {
                return Result.Failure<(Guid, Guid?)>(new Error("Registration.NotFound", "Registration not found"));
            }

            if (registration.Status != Domain.Enums.RegistrationStatus.Pending)
            {
                return Result.Failure<(Guid, Guid?)>(new Error("Registration.NotPending", "Only pending registrations can be approved"));
            }

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // Approve Registration
                registration.Approve();
                await _registrationRepository.UpdateAsync(registration, saveChanges: false);

                // Create Member
                var memberResult = Member.Register(
                    registration.FullName,
                    registration.Email,
                    registration.PhoneNumber,
                    Guid.NewGuid().ToString().Substring(0, 8).ToUpper(), // Generate temp CardId
                    registration.PreferredPlanId);

                if (memberResult.IsFailure) return Result.Failure<(Guid, Guid?)>(memberResult.Error);

                var member = memberResult.Value;
                member.FacilityId = request.FacilityId;
                await _memberRepository.AddAsync(member, saveChanges: false);

                // Create Sale record for registration revenue
                Money planPrice = Money.Zero("DA");
                string planName = "Membership Registration";
                MembershipPlan? plan = null;

                if (registration.PreferredPlanId.HasValue)
                {
                    plan = await _membershipPlanRepository.GetByIdAsync(registration.PreferredPlanId.Value, request.FacilityId);
                    if (plan != null)
                    {
                        planPrice = plan.Price;
                        planName = plan.Name;
                    }
                }

                var saleResult = Sale.Create(
                    memberId: member.Id,
                    paymentMethod: PaymentMethod.Cash,  // default — staff can edit later
                    transactionType: "MembershipRegistration",
                    category: SaleCategory.Membership,
                    capturedLabel: $"Registration — {registration.FullName}");

                Guid? createdSaleId = null;

                if (saleResult.IsSuccess)
                {
                    var saleEntity = saleResult.Value;
                    createdSaleId = saleEntity.Id;
                    saleEntity.FacilityId = request.FacilityId;
                    saleEntity.TenantId = _tenantService.GetTenantId() ?? Guid.Empty;

                    if (plan != null)
                    {
                        // Create a temporary Product adapter for the plan to use AddLineItem
                        var tempProductResult = Management.Domain.Models.Product.Create(
                            plan.Name, 
                            plan.Description, 
                            plan.Price, 
                            Money.Zero("DA"), 
                            999, 
                            "PLAN-" + plan.Id.ToString().Substring(0, 8), 
                            ProductCategory.Other, 
                            "",
                            0);
                            
                        if (tempProductResult.IsSuccess)
                        {
                            var tempProduct = tempProductResult.Value;
                            typeof(Management.Domain.Models.Product).GetProperty("Id")?.SetValue(tempProduct, plan.Id);
                            saleEntity.AddLineItem(tempProduct, 1);
                        }
                    }
                    else
                    {
                        // Fallback generic item if no plan
                        var fallbackProductResult = Management.Domain.Models.Product.Create(
                            "Registration Fee", 
                            "", 
                            planPrice, 
                            Money.Zero("DA"), 
                            999, 
                            "REG", 
                            ProductCategory.Other, 
                            "",
                            0);
                            
                        if (fallbackProductResult.IsSuccess)
                        {
                            saleEntity.AddLineItem(fallbackProductResult.Value, 1);
                        }
                    }

                    // FIX: Use saveChanges: false to ensure all changes stay within the transaction
                    await _saleRepository.AddAsync(saleEntity, saveChanges: false);
                }
                else
                {
                    _logger.LogError("[Registration] Could not create sale for registration {Id}: {Error}. Rolling back.",
                        registration.Id, saleResult.Error);
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<(Guid, Guid?)>(saleResult.Error);
                }

                // Explicitly save all changes and commit
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success((member.Id, createdSaleId));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "[Registration] Transaction failed during registration approval {Id}", registration.Id);
                throw;
            }
        }
    }
}
