using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class TurnstileDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string HardwareId { get; set; }
        public TurnstileStatus Status { get; set; }
        public DateTime LastHeartbeat { get; set; }
    }
}