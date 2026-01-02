using System;
using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record TurnstileDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public TurnstileStatus Status { get; set; } = TurnstileStatus.Operational;
        public DateTime LastHeartbeat { get; set; }
    }
}