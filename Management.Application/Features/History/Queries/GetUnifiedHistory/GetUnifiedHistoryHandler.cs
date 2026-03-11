using Management.Application.Features.History.Queries.GetUnifiedHistory;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.History.Queries.GetUnifiedHistory
{
    public class GetUnifiedHistoryHandler : IRequestHandler<GetUnifiedHistoryQuery, IEnumerable<UnifiedHistoryEventDto>>
    {
        private readonly IAccessEventService _accessEventService;
        private readonly ISaleService _saleService;
        private readonly IReservationService _reservationService;
        private readonly IFacilityContextService _facilityService;

        public GetUnifiedHistoryHandler(
            IAccessEventService accessEventService,
            ISaleService saleService,
            IReservationService reservationService,
            IFacilityContextService facilityService)
        {
            _accessEventService = accessEventService;
            _saleService = saleService;
            _reservationService = reservationService;
            _facilityService = facilityService;
        }

        public async Task<IEnumerable<UnifiedHistoryEventDto>> Handle(GetUnifiedHistoryQuery request, CancellationToken cancellationToken)
        {
            var facilityId = _facilityService.CurrentFacilityId;
            
            // 1. Fetch all streams in parallel (Architecture Requirement: Async Flow Purity)
            var accessTask = _accessEventService.GetEventsByRangeAsync(facilityId, request.StartDate, request.EndDate);
            var saleTask = _saleService.GetSalesByRangeAsync(facilityId, request.StartDate, request.EndDate);
            var reservationTask = _reservationService.GetReservationsByRangeAsync(request.StartDate, request.EndDate);

            await Task.WhenAll(accessTask, saleTask, reservationTask);

            var result = new List<UnifiedHistoryEventDto>();

            // 2. Map Access Events
            var accessResponse = await accessTask;
            if (accessResponse.IsSuccess)
            {
                result.AddRange(accessResponse.Value.Select(x => new UnifiedHistoryEventDto
                {
                    Id = x.Id,
                    Timestamp = x.Timestamp,
                    Type = HistoryEventType.Access,
                    Title = x.IsAccessGranted ? "Access Granted" : "Access Denied",
                    Details = x.IsAccessGranted ? (x.MemberName ?? $"Card: {x.CardId}") : $"Denied ({x.FailureReason}): {x.CardId}",
                    IsSuccessful = x.IsAccessGranted
                }));
            }

            // 3. Map Sales Events
            var salesResponse = await saleTask;
            if (salesResponse.IsSuccess)
            {
                result.AddRange(salesResponse.Value.Select(x => new UnifiedHistoryEventDto
                {
                    Id = x.Id,
                    Timestamp = x.Timestamp,
                    Type = HistoryEventType.Sale,
                    Title = string.IsNullOrEmpty(x.TransactionType) ? "Sale" : x.TransactionType,
                    Details = $"Member: {x.MemberName} - Items: {x.Items.Count}",
                    Amount = x.TotalAmount,
                    Metadata = x.PaymentMethod
                }));
            }

            // 4. Map Reservations
            var reservationResponse = await reservationTask;
            if (reservationResponse.IsSuccess)
            {
                result.AddRange(reservationResponse.Value.Select(x => new UnifiedHistoryEventDto
                {
                    Id = x.Id,
                    Timestamp = x.StartTime,
                    Type = HistoryEventType.Reservation,
                    Title = "Class Booking",
                    Details = $"{x.ActivityName} - {x.InstructorName} ({x.Location})"
                }));
            }

            // 5. Final Sort (Newest First)
            return result.OrderByDescending(x => x.Timestamp);
        }
    }
}
