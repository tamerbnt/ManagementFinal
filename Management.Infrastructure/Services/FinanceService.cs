using Management.Application.Features.Finance.Queries.GetPayrollHistory;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class FinanceService : IFinanceService
    {
        private readonly ISender _sender;
        private readonly IPayrollRepository _payrollRepository;

        public FinanceService(ISender sender, IPayrollRepository payrollRepository)
        {
            _sender = sender;
            _payrollRepository = payrollRepository;
        }

        public async Task<Result<FinancialMetricsDto>> GetDashboardMetricsAsync(Guid facilityId)
        {
            // Placeholder: This would aggregate data from multiple sources
            var metrics = new FinancialMetricsDto(
                MonthlyRevenue: 0,
                RevenueGrowth: 0,
                Trend: TrendDirection.Stable,
                MRR: 0,
                ARPU: 0,
                ChurnRate: 0,
                NewMembers: 0,
                TotalMembers: 0,
                SuccessRate: 0,
                RevenueSparkline: new List<ChartPointDto>()
            );
            return Result.Success(metrics);
        }

        public async Task<Result<List<FailedPaymentDto>>> GetFailedPaymentsAsync(Guid facilityId)
        {
            // Placeholder: This would query failed transactions
            return Result.Success(new List<FailedPaymentDto>());
        }

        public async Task<Result> RetryPaymentAsync(Guid facilityId, Guid paymentId)
        {
            // Placeholder: This would retry a payment via gateway
            return Result.Success();
        }

        public async Task<Result<IEnumerable<PayrollEntryDto>>> GetPayrollByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            try
            {
                var entries = await _payrollRepository.GetByRangeAsync(facilityId, start, end);
                var dtos = entries.Select(e => new PayrollEntryDto
                {
                    Id = e.Id,
                    StaffId = e.StaffId,
                    StaffName = "Staff Member", // In a real app we'd join but keep it simple for now
                    PayPeriodStart = e.PayPeriodStart,
                    PayPeriodEnd = e.PayPeriodEnd,
                    Amount = e.Amount.Amount,
                    NetPay = e.PaidAmount.Amount,
                    IsPaid = e.IsPaid,
                    PaymentMethod = "Bank Transfer", // Default
                    ProcessedAt = e.UpdatedAt ?? e.CreatedAt,
                    Notes = string.Empty
                });
                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<IEnumerable<PayrollEntryDto>>(new Error("Finance.PayrollLoadFailed", ex.Message));
            }
        }
    }
}
