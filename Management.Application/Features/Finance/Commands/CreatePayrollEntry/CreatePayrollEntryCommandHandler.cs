using Management.Application.DTOs;
using Management.Application.Notifications;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Finance.Commands.CreatePayrollEntry
{
    public class CreatePayrollEntryCommandHandler : IRequestHandler<CreatePayrollEntryCommand, Result<Guid>>
    {
        private readonly IPayrollRepository _payrollRepository;
        private readonly IStaffRepository _staffRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly ILogger<CreatePayrollEntryCommandHandler> _logger;

        public CreatePayrollEntryCommandHandler(
            IPayrollRepository payrollRepository, 
            IStaffRepository staffRepository,
            IUnitOfWork unitOfWork,
            IMediator mediator,
            ILogger<CreatePayrollEntryCommandHandler> logger)
        {
            _payrollRepository = payrollRepository;
            _staffRepository = staffRepository;
            _unitOfWork = unitOfWork;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreatePayrollEntryCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Entry;
            
            // Validate Staff Existence
            var staff = await _staffRepository.GetByIdAsync(dto.StaffId);
            if (staff == null)
            {
                return Result.Failure<Guid>(new Error("Payroll.StaffNotFound", $"Staff member with ID {dto.StaffId} not found."));
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var amount = new Money(dto.Amount, "DA");

                var result = PayrollEntry.Create(
                    dto.StaffId,
                    dto.PayPeriodStart,
                    dto.PayPeriodEnd,
                    amount,
                    dto.BaseSalary,
                    dto.AbsenceCount,
                    dto.AbsenceDeduction);

                if (result.IsFailure) return Result.Failure<Guid>(result.Error);

                var entry = result.Value;

                // ADD THIS — call MarkAsPaid before saving if this is an actual payment
                if (request.IsPaid)
                {
                    entry.MarkAsPaid();
                }

                await _payrollRepository.AddAsync(entry);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // AFTER commit — safe to notify
                // Use fire-and-forget Task.Run ONLY after commit is confirmed
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _mediator.Publish(new FacilityActionCompletedNotification(
                            entry.FacilityId,
                            "Payroll",
                            staff.FullName, // Using staff.FullName as entry doesn't have StaffName
                            "Payroll entry created",
                            entry.Id.ToString()), CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[Payroll] Failed to publish notification");
                    }
                });

                return Result.Success(entry.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to create payroll entry for staff {StaffId}", dto.StaffId);
                return Result.Failure<Guid>(new Error("Payroll.CreationError", "An unexpected error occurred while creating the payroll entry."));
            }
        }
    }
}
