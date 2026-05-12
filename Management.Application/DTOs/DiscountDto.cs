using System;

namespace Management.Application.DTOs
{
    public class DiscountDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Value { get; set; }
        public bool IsPercentage { get; set; }
        public bool IsActive { get; set; }

        public string DiscountType => IsPercentage ? "Percentage" : "Fixed Amount";
        public string DisplayValue => IsPercentage ? $"{Value}%" : $"{Value:N0} DA";
    }
}
