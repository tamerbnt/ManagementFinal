using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public record ZoneDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public FacilityType Type { get; set; }
        public bool IsOperational { get; set; }
    }
}