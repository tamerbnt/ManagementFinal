using System;

namespace Management.Domain.DTOs
{
    public record AccessEventDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid TurnstileId { get; set; }
        public string CardId { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public bool IsAccessGranted { get; set; }
        public string AccessStatus { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public Management.Domain.Enums.FacilityType FacilityType { get; set; }
    }
}