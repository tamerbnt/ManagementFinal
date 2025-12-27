using System;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a physical hardware entry point.
    /// </summary>
    public class Turnstile : Entity
    {
        public string Name { get; set; } // "Front Gate"
        public string HardwareId { get; set; } // IP or Serial Number
        public TurnstileStatus Status { get; set; }
        public DateTime LastHeartbeat { get; set; }
    }
}