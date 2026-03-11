using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Domain.Models.Salon;

namespace Management.Application.Services.History
{
    /// <summary>
    /// Salon-specific implementation of IHistoryProvider.
    /// Consolidates appointments, sales, and payroll payments.
    /// </summary>
    public class SalonHistoryProvider : IHistoryProvider
    {
        private readonly ISaleService _saleService;
        private readonly IAppointmentService _appointmentService;
        private readonly IFinanceService _financeService;

        public string SegmentName => "Salon";

        public SalonHistoryProvider(
            ISaleService saleService,
            IAppointmentService appointmentService,
            IFinanceService financeService)
        {
            _saleService = saleService;
            _appointmentService = appointmentService;
            _financeService = financeService;
        }

        public async Task<IEnumerable<UnifiedHistoryEventDto>> GetHistoryAsync(Guid facilityId, DateTime startDate, DateTime endDate)
        {
            // Fetch relevant salon data
            var saleTask = _saleService.GetSalesByRangeAsync(facilityId, startDate, endDate);
            var appointmentTask = _appointmentService.GetByRangeAsync(facilityId, startDate, endDate);
            var payrollTask = _financeService.GetPayrollByRangeAsync(facilityId, startDate, endDate);

            await Task.WhenAll(saleTask, appointmentTask, payrollTask);

            var salesResult = await saleTask;
            var appointments = await appointmentTask;
            var payrollResult = await payrollTask;

            var unifiedEvents = new List<UnifiedHistoryEventDto>();

            // 1. Map Sales
            if (salesResult.IsSuccess)
            {
                foreach (var sale in salesResult.Value)
                {
                    unifiedEvents.Add(new UnifiedHistoryEventDto
                    {
                        Id = sale.Id,
                        Timestamp = sale.Timestamp,
                        Type = HistoryEventType.Sale,
                        Title = $"Sale: {sale.TransactionType}",
                        Details = $"{sale.MemberName} - {string.Join(", ", sale.ItemsSnapshot.Keys)}",
                        Amount = sale.TotalAmount,
                        Metadata = sale.PaymentMethod
                    });
                }
            }

            // 2. Map Appointments
            foreach (var app in appointments)
            {
                unifiedEvents.Add(new UnifiedHistoryEventDto
                {
                    Id = app.Id,
                    Timestamp = app.StartTime,
                    Type = HistoryEventType.Appointment,
                    Title = app.ServiceName,
                    Details = $"{app.ClientName} with {app.StaffName} ({app.Status})",
                    IsSuccessful = app.Status != AppointmentStatus.NoShow && app.Status != AppointmentStatus.Cancelled
                });
            }

            // 3. Map Payroll Payments
            if (payrollResult.IsSuccess)
            {
                foreach (var payroll in payrollResult.Value)
                {
                    unifiedEvents.Add(new UnifiedHistoryEventDto
                    {
                        Id = payroll.Id,
                        Timestamp = payroll.ProcessedAt ?? payroll.PayPeriodEnd,
                        Type = HistoryEventType.Payroll,
                        Title = $"Payroll: {payroll.StaffName}",
                        Details = $"Paid via {payroll.PaymentMethod}",
                        Amount = payroll.NetPay,
                        Metadata = payroll.PaymentMethod
                    });
                }
            }

            return unifiedEvents.OrderByDescending(e => e.Timestamp);
        }
    }
}
