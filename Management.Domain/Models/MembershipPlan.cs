using System;

namespace Management.Domain.Models
{
    /// <summary>
    /// Configuration for a pricing tier (e.g. "Gold - 1 Year").
    /// </summary>
    public class MembershipPlan : Entity
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int DurationMonths { get; set; }
        public bool IsActive { get; set; }
        // Could add JSON blob for specific facility access rights
    }
}