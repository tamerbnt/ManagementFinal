using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Commands.UndoApproveRegistration
{
    public class UndoApproveRegistrationCommandHandler : IRequestHandler<UndoApproveRegistrationCommand, Result>
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IMemberRepository _memberRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UndoApproveRegistrationCommandHandler> _logger;

        public UndoApproveRegistrationCommandHandler(
            IRegistrationRepository registrationRepository,
            IMemberRepository memberRepository,
            ISaleRepository saleRepository,
            IUnitOfWork unitOfWork,
            ILogger<UndoApproveRegistrationCommandHandler> logger)
        {
            _registrationRepository = registrationRepository;
            _memberRepository = memberRepository;
            _saleRepository = saleRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(UndoApproveRegistrationCommand request, CancellationToken cancellationToken)
        {
            var registration = await _registrationRepository.GetByIdAsync(request.RegistrationId, request.FacilityId);
            if (registration == null)
            {
                return Result.Failure(new Error("Registration.NotFound", "Registration not found"));
            }

            if (registration.Status != RegistrationStatus.Approved)
            {
                return Result.Failure(new Error("Registration.NotApproved", "Only approved registrations can be undone"));
            }

            // Begin transaction
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Revert Registration Status
                registration.RevertToPending();
                await _registrationRepository.UpdateAsync(registration, saveChanges: false);

                // 2. Find the Member created from this registration
                // Since Member has same email and was created very recently, we try to match by Email
                // A more robust way is if Approve Registration returned the MemberId, but here we must query.
                var allMembers = (await _memberRepository.SearchAsync("", request.FacilityId)).ToList();
                var createdMember = allMembers.OrderByDescending(m => m.CreatedAt).FirstOrDefault(m => m.Email.Value == registration.Email.Value);

                if (createdMember != null)
                {
                    // 3. Find Sales created for this Member exactly for registration
                    var sales = await _saleRepository.GetSalesByMemberAsync(createdMember.Id, request.FacilityId);
                    var registrationSale = sales.FirstOrDefault(s => s.TransactionType == "MembershipRegistration");

                    if (registrationSale != null)
                    {
                        // Delete the sale
                        await _saleRepository.DeleteAsync(registrationSale.Id, saveChanges: false);
                    }

                    // Delete the member
                    await _memberRepository.DeleteAsync(createdMember.Id, saveChanges: false);
                }

                // Explicitly save all changes and commit
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                
                return Result.Success();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "[Registration] Transaction failed during undo registration approval {Id}", registration.Id);
                throw;
            }
        }
    }
}
