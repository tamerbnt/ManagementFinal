using System;

namespace Management.Domain.DTOs
{
    public enum HistoryEventType
    {
        Access,
        Sale,
        Reservation
    }

    public record UnifiedHistoryEventDto
    {
        public Guid Id { get; init; }
        public DateTime Timestamp { get; init; }
        public HistoryEventType Type { get; init; }
        public AccessEventDto? AccessEvent { get; init; }
        public SaleDto? SaleEvent { get; init; }
        public ReservationDto? ReservationEvent { get; init; }
    }
}
