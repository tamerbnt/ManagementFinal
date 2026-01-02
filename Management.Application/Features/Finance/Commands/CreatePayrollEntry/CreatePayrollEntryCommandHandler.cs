using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Finance.Commands.CreatePayrollEntry
{
    public class CreatePayrollEntryCommandHandler : IRequestHandler<CreatePayrollEntryCommand, Result<Guid>>
    {
        private readonly IPayrollRepository _payrollRepository;

        public CreatePayrollEntryCommandHandler(IPayrollRepository payrollRepository)
        {
            _payrollRepository = payrollRepository;
        }

        public async Task<Result<Guid>> Handle(CreatePayrollEntryCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Entry;
            var amount = new Money(dto.Amount, "USD");

            var result = PayrollEntry.Create(
                dto.StaffId,
                dto.PayPeriodStart,
                dto.PayPeriodEnd,
                amount);

            if (result.IsFailure) return Result.Failure<Guid>(result.Error);

            var entry = result.Value;
            await _payrollRepository.AddAsync(entry);

            return Result.Success(entry.Id);
        }
    }
}
