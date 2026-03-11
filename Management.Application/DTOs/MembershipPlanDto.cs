using System;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record MembershipPlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSessionPack { get; set; }
        public bool IsWalkIn { get; set; }
        public int GenderRule { get; set; }
        public string? ScheduleJson { get; set; }
    }
}
