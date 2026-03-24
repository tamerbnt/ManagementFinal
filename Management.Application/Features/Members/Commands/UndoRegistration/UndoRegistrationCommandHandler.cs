using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Commands.UndoRegistration
{
    public class UndoRegistrationCommandHandler : IRequestHandler<UndoRegistrationCommand, Result>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UndoRegistrationCommandHandler(
            IMemberRepository memberRepository,
            ISaleRepository saleRepository,
            IUnitOfWork unitOfWork)
        {
            _memberRepository = memberRepository;
            _saleRepository = saleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UndoRegistrationCommand request, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"[UNDO] UndoRegistrationCommandHandler.Handle started. MemberId={request.MemberId}");

            // 1. Get the member (Tracked)
            var member = await _memberRepository.GetByIdAsync(request.MemberId, request.FacilityId);
            System.Diagnostics.Debug.WriteLine($"[UNDO] Member lookup: {(member == null ? "NULL — not found" : $"Found: {member.FullName}, IsDeleted={member.IsDeleted}")}");

            if (member == null)
            {
                System.Diagnostics.Debug.WriteLine("[UNDO] Member is null — returning success (idempotent)");
                return Result.Success();
            }

            // 2. Get associated sales WITH items (Tracked)
            // We use the new ForUndo method which doesn't use AsNoTracking()
            var sales = await _saleRepository.GetSalesByMemberForUndoAsync(request.MemberId, request.FacilityId);
            System.Diagnostics.Debug.WriteLine($"[UNDO] Sales found for undo: {sales?.Count() ?? 0}");

            System.Diagnostics.Debug.WriteLine("[UNDO] Starting transaction for soft-delete");
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // 3. Delete each sale
                // Since they are tracked with their items, the Interceptor will catch everything
                // and perform a soft-delete (Modified state + IsDeleted=true) for both parent and children.
                foreach (var sale in sales)
                {
                    System.Diagnostics.Debug.WriteLine($"[UNDO] Soft-deleting sale {sale.Id}");
                    await _saleRepository.DeleteAsync(sale.Id, saveChanges: false);
                }

                // 4. Delete Member
                System.Diagnostics.Debug.WriteLine($"[UNDO] Soft-deleting member {member.Id}");
                await _memberRepository.DeleteAsync(member.Id, saveChanges: false);

                // 5. Commit everything atomically
                System.Diagnostics.Debug.WriteLine("[UNDO] Calling SaveChangesAsync");
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine("[UNDO] Calling CommitAsync");
                await transaction.CommitAsync(cancellationToken);

                System.Diagnostics.Debug.WriteLine("[UNDO] Handle completed successfully");
                return Result.Success();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UNDO] EXCEPTION in undo transaction: {ex}");
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
