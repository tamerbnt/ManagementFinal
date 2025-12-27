using System;

namespace Management.Domain.Models
{
    /// <summary>
    /// Stores global configuration. Typically only one row exists in this table.
    /// </summary>
    public class GymSettings : Entity
    {
        // General Settings
        public string GymName { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Website { get; set; }
        public string TaxId { get; set; }
        public string LogoUrl { get; set; }

        // Facility Settings
        public int MaxOccupancy { get; set; }
        public bool IsMaintenanceMode { get; set; }

        // Serialized JSON for complex nested structures (Schedule, etc.)
        // This avoids creating 4-5 tiny tables for configuration data.
        public string OperatingHoursJson { get; set; }
    }
}