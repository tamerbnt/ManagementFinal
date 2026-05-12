using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class Discount : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Value { get; set; }
        public bool IsPercentage { get; set; }
        public bool IsActive { get; set; }

        public Discount() { }

        private Discount(
            Guid id,
            string name,
            string? description,
            decimal value,
            bool isPercentage) : base(id)
        {
            Name = name;
            Description = description;
            Value = value;
            IsPercentage = isPercentage;
            IsActive = true;
        }

        public static Result<Discount> Create(
            string name,
            string? description,
            decimal value,
            bool isPercentage)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<Discount>(new Error("Discount.EmptyName", "Discount name is required."));

            if (value < 0)
                return Result.Failure<Discount>(new Error("Discount.NegativeValue", "Discount value cannot be negative."));

            var discount = new Discount(
                Guid.NewGuid(),
                name,
                description,
                value,
                isPercentage);

            return Result.Success(discount);
        }

        public void UpdateDetails(
            string name,
            string? description,
            decimal value,
            bool isPercentage,
            bool isActive)
        {
            Name = name;
            Description = description;
            Value = value;
            IsPercentage = isPercentage;
            IsActive = isActive;
            UpdateTimestamp();
        }
    }
}
