using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Finance.Commands.MarkPayrollPaid
{
    public class MarkPayrollPaidCommandHandler : IRequestHandler<MarkPayrollPaidCommand, Result>
    {
        private readonly IPayrollRepository _payrollRepository;

        public MarkPayrollPaidCommandHandler(IPayrollRepository payrollRepository)
        {
            _payrollRepository = payrollRepository;
        }

        public async Task<Result> Handle(MarkPayrollPaidCommand request, CancellationToken cancellationToken)
        {
            var entry = await _payrollRepository.GetByIdAsync(request.EntryId);
            if (entry == null) return Result.Failure(new Error("Payroll.NotFound", "Payroll entry not found"));

            entry.MarkAsPaid();
            await _payrollRepository.UpdateAsync(entry);

            return Result.Success();
        }
    }
}
