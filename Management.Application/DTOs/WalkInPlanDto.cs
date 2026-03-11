using System;

namespace Management.Application.DTOs
{
    public class WalkInPlanDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string DurationDescription { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
    }
}
