using System;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    /// <summary>
    /// Configuration for a pricing tier (e.g. "Gold - 1 Year").
    /// </summary>
    public class MembershipPlan : AggregateRoot
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int DurationDays { get; private set; } // e.g., 30, 365
        public Money Price { get; private set; }
        public bool IsActive { get; private set; }

        private MembershipPlan(Guid id, string name, string description, int durationDays, Money price) : base(id)
        {
            Name = name;
            Description = description;
            DurationDays = durationDays;
            Price = price;
            IsActive = true;
        }

        private MembershipPlan() 
        {
            Name = default!;
            Description = default!;
            Price = default!;
        }

        public static Result<MembershipPlan> Create(string name, string description, int durationDays, Money price)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<MembershipPlan>(new Error("Plan.EmptyName", "Name is required"));
            
            if (durationDays <= 0)
                return Result.Failure<MembershipPlan>(new Error("Plan.InvalidDuration", "Duration must be positive"));

            return Result.Success(new MembershipPlan(Guid.NewGuid(), name, description, durationDays, price));
        }

        public void UpdateDetails(string name, string description, int durationDays, Money price)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty");
            
            Name = name;
            Description = description;
            DurationDays = durationDays;
            Price = price;
            UpdateTimestamp();
        }

        public void UpdatePricing(Money newPrice)
        {
            Price = newPrice;
            UpdateTimestamp();
        }
        
        public void Activate() { IsActive = true; UpdateTimestamp(); }
        public void Deactivate() { IsActive = false; UpdateTimestamp(); }
    }
}