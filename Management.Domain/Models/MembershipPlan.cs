using System;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    /// <summary>
    /// Configuration for a pricing tier (e.g. "Gold - 1 Year").
    /// </summary>
    public class MembershipPlan : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int DurationDays { get; set; } // e.g., 30, 365
        public Money Price { get; set; }
        public bool IsActive { get; set; }
        public virtual System.Collections.Generic.ICollection<Management.Domain.Models.Facility> AccessibleFacilities { get; set; } = new System.Collections.Generic.List<Management.Domain.Models.Facility>();
        public bool IsSessionPack { get; set; }
        public int BaseSessionCount { get; private set; }
        public bool IsWalkIn { get; set; }

        // Behavioral Rules
        public int GenderRule { get; set; } // 0: Both, 1: MaleOnly, 2: FemaleOnly
        public string? ScheduleJson { get; set; } // JSON serialized schedule windows

        private MembershipPlan(Guid id, string name, string description, int durationDays, Money price, int baseSessionCount = 0, bool isWalkIn = false) : base(id)
        {
            Name = name;
            Description = description;
            DurationDays = durationDays;
            Price = price;
            BaseSessionCount = baseSessionCount;
            IsActive = true;
            IsWalkIn = isWalkIn;
        }

        public MembershipPlan() 
        {
            Name = default!;
            Description = default!;
            Price = default!;
        }

        public static Result<MembershipPlan> Create(string name, string description, int durationDays, Money price, int baseSessionCount = 0, bool isWalkIn = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<MembershipPlan>(new Error("Plan.EmptyName", "Name is required"));
            
            if (durationDays <= 0)
                return Result.Failure<MembershipPlan>(new Error("Plan.InvalidDuration", "Duration must be positive"));

            return Result.Success(new MembershipPlan(Guid.NewGuid(), name, description, durationDays, price, baseSessionCount, isWalkIn));
        }

        public void UpdateDetails(string name, string description, int durationDays, Money price, int? baseSessionCount = null, bool? isWalkIn = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty");
            
            Name = name;
            Description = description;
            DurationDays = durationDays;
            Price = price;
            if (baseSessionCount.HasValue) BaseSessionCount = baseSessionCount.Value;
            if (isWalkIn.HasValue) IsWalkIn = isWalkIn.Value;
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
