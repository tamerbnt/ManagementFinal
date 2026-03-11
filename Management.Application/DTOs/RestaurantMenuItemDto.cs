using System;

namespace Management.Application.DTOs
{
    public class RestaurantMenuItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public string[] Ingredients { get; set; } = Array.Empty<string>();
        public Guid FacilityId { get; set; }
        public Guid TenantId { get; set; }
    }
}
