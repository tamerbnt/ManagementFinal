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
using MediatR;

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
        private readonly ISender _sender;

        public string SegmentName => "Gym";

        public GymHistoryProvider(
            ITransactionService transactionService,
            ISaleService saleService,
            IAppointmentService appointmentService,
            IAccessEventService accessEventService,
            IReservationService reservationService,
            IFinanceService financeService,
            ISender sender)
        {
            _transactionService = transactionService;
            _saleService = saleService;
            _appointmentService = appointmentService;
            _accessEventService = accessEventService;
            _reservationService = reservationService;
            _financeService = financeService;
            _sender = sender;
        }

        public async Task<IEnumerable<UnifiedHistoryEventDto>> GetHistoryAsync(Guid facilityId, DateTime startDate, DateTime endDate, bool includeDeleted = false)
        {
            // FIX: Use sequential execution to prevent EF Core DbContext concurrency exceptions.
            // Even with a fresh scope per refresh, the individual service calls in GymHistoryProvider
            // attempt to use the SAME DbContext instance concurrently if Task.WhenAll is used.
            var transactionsResult = await _transactionService.GetHistoryByRangeAsync(facilityId, startDate, endDate, includeDeleted);
            var salesResult = await _sender.Send(new Management.Application.Features.Sales.Queries.GetSales.GetSalesHistoryQuery { 
                FacilityId = facilityId, 
                Start = startDate, 
                End = endDate, 
                IncludeDeleted = includeDeleted 
            });
            var appointments = await _appointmentService.GetByRangeAsync(facilityId, startDate, endDate);
            var accessResult = await _sender.Send(new Management.Application.Features.Turnstiles.Queries.GetAccessEventsQuery(facilityId, null, startDate, includeDeleted));
            var reservationsResult = await _reservationService.GetReservationsByRangeAsync(startDate, endDate);
            var payrollResult = await _financeService.GetPayrollByRangeAsync(facilityId, startDate, endDate);

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
                        TitleLocalizationKey = tx.Items.Count > 1 ? "Terminology.History.Event.MultiplePurchase" : null,
                        Title = tx.Items.Count > 1 ? "Multiple Items Purchase" : (tx.Items.FirstOrDefault()?.ProductName ?? "Shop Sale"),
                        Details = string.Join(", ", tx.Items.Select(i => i.ProductName)),
                        Amount = tx.TotalAmount,
                        Metadata = tx.PaymentMethod.ToString(),
                        AuditNote = tx.AuditNote,
                        IsDeleted = tx.IsDeleted
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
                        TitleLocalizationKey = "Terminology.History.Event.Sale",
                        Title = $"Sale: {sale.TransactionType}",
                        Details = $"{(string.IsNullOrEmpty(sale.MemberName) ? "Guest" : sale.MemberName)} - {string.Join(", ", sale.ItemsSnapshot.Keys)}",
                        Amount = sale.TotalAmount,
                        Metadata = sale.PaymentMethod,
                        IsDeleted = sale.IsDeleted
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
                    TitleLocalizationKey = "Terminology.History.Event.Appointment",
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
                        TitleLocalizationKey = ae.IsAccessGranted ? "Terminology.History.Event.CheckIn" : "Terminology.History.Event.AccessDenied",
                        Title = ae.IsAccessGranted ? "Check-in" : "Access Denied",
                        Details = ae.IsAccessGranted ? (ae.MemberName ?? $"Card: {ae.CardId}") : $"Denied ({ae.FailureReason}): {ae.CardId}",
                        IsSuccessful = ae.IsAccessGranted,
                        IsDeleted = ae.IsDeleted
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
                        TitleLocalizationKey = "Terminology.History.Event.Payroll",
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
