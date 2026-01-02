using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Finance.Queries.GetPayrollHistory
{
    public class GetPayrollHistoryQueryHandler : IRequestHandler<GetPayrollHistoryQuery, Result<List<PayrollEntryDto>>>
    {
        private readonly IPayrollRepository _payrollRepository;
        private readonly IStaffRepository _staffRepository;

        public GetPayrollHistoryQueryHandler(IPayrollRepository payrollRepository, IStaffRepository staffRepository)
        {
            _payrollRepository = payrollRepository;
            _staffRepository = staffRepository;
        }

        public async Task<Result<List<PayrollEntryDto>>> Handle(GetPayrollHistoryQuery request, CancellationToken cancellationToken)
        {
            var entries = await _payrollRepository.GetAllAsync();
            var dtos = new List<PayrollEntryDto>();

            foreach (var entry in entries)
            {
                var staff = await _staffRepository.GetByIdAsync(entry.StaffMemberId);
                dtos.Add(new PayrollEntryDto
                {
                    Id = entry.Id,
                    StaffId = entry.StaffMemberId,
                    StaffName = staff?.FullName ?? "Unknown Staff",
                    Amount = entry.Amount.Amount,
                    PayPeriodStart = entry.PayPeriodStart,
                    PayPeriodEnd = entry.PayPeriodEnd,
                    IsPaid = entry.IsPaid
                });
            }

            return Result.Success(dtos.OrderByDescending(x => x.PayPeriodEnd).ToList());
        }
    }
}
