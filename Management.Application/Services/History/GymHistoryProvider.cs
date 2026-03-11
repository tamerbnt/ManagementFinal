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
    /// Gym-specific implementation of IHistoryProvider.
    /// Consolidates transactions, access events, and class reservations.
    /// </summary>
    public class GymHistoryProvider : IHistoryProvider
    {
        private readonly ITransactionService _transactionService;
        private readonly ISaleService _saleService;
        private readonly IAppointmentService _appointmentService;
        private readonly IAccessEventService _accessEventService;
        private readonly IReservationService _reservationService;
        private readonly IFinanceService _financeService;

        public string SegmentName => "Gym";

        public GymHistoryProvider(
            ITransactionService transactionService,
            ISaleService saleService,
            IAppointmentService appointmentService,
            IAccessEventService accessEventService,
            IReservationService reservationService,
            IFinanceService financeService)
        {
            _transactionService = transactionService;
            _saleService = saleService;
            _appointmentService = appointmentService;
            _accessEventService = accessEventService;
            _reservationService = reservationService;
            _financeService = financeService;
        }

        public async Task<IEnumerable<UnifiedHistoryEventDto>> GetHistoryAsync(Guid facilityId, DateTime startDate, DateTime endDate)
        {
            // Parallel fetch for performance using new range-based methods
            var transactionTask = _transactionService.GetHistoryByRangeAsync(facilityId, startDate, endDate);
            var saleTask = _saleService.GetSalesByRangeAsync(facilityId, startDate, endDate);
            var appointmentTask = _appointmentService.GetByRangeAsync(facilityId, startDate, endDate);
            var accessTask = _accessEventService.GetEventsByRangeAsync(facilityId, startDate, endDate);
            
            // For Salon, Appointments are the main source of bookings.
            var reservationTask = _reservationService.GetReservationsByRangeAsync(startDate, endDate);
            var payrollTask = _financeService.GetPayrollByRangeAsync(facilityId, startDate, endDate);

            await Task.WhenAll(transactionTask, saleTask, appointmentTask, accessTask, reservationTask, payrollTask);

            var transactionsResult = await transactionTask;
            var salesResult = await saleTask;
            var appointments = await appointmentTask;
            var accessResult = await accessTask;
            var reservationsResult = await reservationTask;
            var payrollResult = await payrollTask;

            var unifiedEvents = new List<UnifiedHistoryEventDto>();

            // Map Transactions (Shop Sales)
            if (transactionsResult.IsSuccess)
            {
                foreach (var tx in transactionsResult.Value)
                {
                    unifiedEvents.Add(new UnifiedHistoryEventDto
                    {
                        Id = tx.Id,
                        Timestamp = tx.Timestamp,
                        Type = HistoryEventType.Payment,
                        Title = tx.Items.Count > 1 ? "Multiple Items Purchase" : (tx.Items.FirstOrDefault()?.ProductName ?? "Shop Sale"),
                        Details = string.Join(", ", tx.Items.Select(i => i.ProductName)),
                        Amount = tx.TotalAmount,
                        Metadata = tx.PaymentMethod.ToString(),
                        AuditNote = tx.AuditNote
                    });
                }
            }

            // Map Sales (Cashing Out Services/Products)
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

            // Map Appointments
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

            // Map Access Events
            if (accessResult.IsSuccess)
            {
                foreach (var ae in accessResult.Value)
                {
                    unifiedEvents.Add(new UnifiedHistoryEventDto
                    {
                        Id = ae.Id,
                        Timestamp = ae.Timestamp,
                        Type = HistoryEventType.Access,
                        Title = ae.IsAccessGranted ? "Check-in" : "Access Denied",
                        Details = ae.IsAccessGranted ? (ae.MemberName ?? $"Card: {ae.CardId}") : $"Denied ({ae.FailureReason}): {ae.CardId}",
                        IsSuccessful = ae.IsAccessGranted
                    });
                }
            }

            // Map Class Reservations (Legacy or Gym-specific)
            if (reservationsResult.IsSuccess)
            {
                foreach (var res in reservationsResult.Value)
                {
                    unifiedEvents.Add(new UnifiedHistoryEventDto
                    {
                        Id = res.Id,
                        Timestamp = res.StartTime,
                        Type = HistoryEventType.Reservation,
                        Title = "Course/Registration",
                        Details = $"{res.ActivityName} - {res.InstructorName} ({res.Location})"
                    });
                }
            }

            // Map Payroll Payments
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
