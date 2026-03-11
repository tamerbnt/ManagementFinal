using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class GymSettings : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        // --- 1. GENERAL ---
        public string GymName { get; set; } = "My Gym";
        public string Email { get; set; } = "contact@gym.com";
        public string PhoneNumber { get; set; } = "555-0100";
        public string Website { get; set; } = "https://mygym.com";
        public string TaxId { get; set; } = "TAX123";
        public string Address { get; set; } = "123 Main St";
        public string LogoUrl { get; set; } = "";

        // --- 2. FACILITY ---
        public int MaxOccupancy { get; set; } = 100;
        public decimal DailyRevenueTarget { get; set; } = 10000m; // Default target
        public bool IsMaintenanceMode { get; set; } = false;
        public string OperatingHoursJson { get; set; } = "{}";

        // --- 3. APPEARANCE ---
        public bool IsLightMode { get; set; } = true;
        public string Language { get; set; } = "en-US";
        public string DateFormat { get; set; } = "MM/dd/yyyy";
        public bool HighContrast { get; set; } = false;
        public bool ReducedMotion { get; set; } = false;
        public string TextScale { get; set; } = "100%";
    }
}
