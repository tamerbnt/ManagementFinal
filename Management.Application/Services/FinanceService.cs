using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Services;

namespace Management.Application.Services
{
    public class FinanceService : IFinanceService
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IMemberRepository _memberRepository;
        private readonly IPayrollRepository _payrollRepository;

        public FinanceService(
            ISaleRepository saleRepository,
            IMemberRepository memberRepository,
            IPayrollRepository payrollRepository)
        {
            _saleRepository = saleRepository;
            _memberRepository = memberRepository;
            _payrollRepository = payrollRepository;
        }

        public async Task<FinancialMetricsDto> GetDashboardMetricsAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfPrevMonth = startOfMonth.AddMonths(-1);

            // 1. Revenue
            var currentRevenue = await _saleRepository.GetTotalRevenueAsync(startOfMonth, now);
            var prevRevenue = await _saleRepository.GetTotalRevenueAsync(startOfPrevMonth, startOfMonth);

            double growth = 0;
            if (prevRevenue > 0)
                growth = (double)((currentRevenue - prevRevenue) / prevRevenue) * 100;

            // 2. Member Stats
            var activeMembers = await _memberRepository.GetActiveCountAsync();
            var totalMembers = await _memberRepository.GetTotalCountAsync();

            // MRR Estimate (Avg $50/member mock calculation)
            // Real impl would sum up MembershipPlan prices for all active members
            decimal mrr = activeMembers * 50.00m;

            // 3. Sparkline (Mock Data for Visualization)
            var sparkline = new List<ChartPointDto>();
            for (int i = 0; i < 10; i++)
            {
                sparkline.Add(new ChartPointDto { X = i * 10, Y = (double)(currentRevenue / 10) + new Random().Next(-500, 500) });
            }

            return new FinancialMetricsDto
            {
                MonthlyRevenue = currentRevenue,
                RevenueGrowth = Math.Round(growth, 1),
                Trend = growth >= 0 ? TrendDirection.Up : TrendDirection.Down,
                MRR = mrr,
                ARPU = activeMembers > 0 ? (mrr / activeMembers) : 0,
                ChurnRate = 2.5, // Hardcoded or calculated from Expired vs Total
                NewMembers = 15, // Would query MemberRepository.GetNewCount(month)
                TotalMembers = totalMembers,
                SuccessRate = 98.5,
                RevenueSparkline = sparkline
            };
        }

        public async Task<List<FailedPaymentDto>> GetFailedPaymentsAsync()
        {
            var failedSales = await _saleRepository.GetFailedTransactionsAsync();

            return failedSales.Select(s => new FailedPaymentDto
            {
                Id = s.Id,
                Amount = s.TotalAmount,
                MemberName = "Unknown", // Would need Include(s => s.Member) in Repo
                Reason = "Card Declined", // Mock reason
                AttemptDate = s.Timestamp
            }).ToList();
        }

        public async Task<bool> RetryPaymentAsync(Guid paymentId)
        {
            // Simulate gateway call
            await Task.Delay(1000);
            return true; // Success
        }
    }
}