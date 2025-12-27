using System;

namespace Management.Domain.DTOs
{
    public class MembershipPlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int DurationMonths { get; set; }
        public bool IsActive { get; set; }
    }
}