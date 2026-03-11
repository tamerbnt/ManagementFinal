using System;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public enum HistoryEventType
    {
        Access,
        Sale,
        Reservation,
        Payment,
        Appointment,
        Order,
        Payroll,
        Inventory
    }

    public record UnifiedHistoryEventDto
    {
        public Guid Id { get; init; }
        public DateTime Timestamp { get; init; }
        public HistoryEventType Type { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? TitleLocalizationKey { get; init; }
        public string[]? TitleLocalizationArgs { get; init; }
        public string Details { get; init; } = string.Empty;
        public string? DetailsLocalizationKey { get; init; }
        public string[]? DetailsLocalizationArgs { get; init; }
        public decimal? Amount { get; init; }
        public bool IsSuccessful { get; init; } = true;
        public string? AuditNote { get; init; }
        public string? Metadata { get; init; }
    }
}
