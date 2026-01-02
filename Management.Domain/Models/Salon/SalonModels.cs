using System.Collections.Generic;
using Management.Domain.Primitives;

namespace Management.Domain.Models.Salon
{
    public enum AppointmentStatus
    {
        Scheduled,
        Confirmed,
        InProgress,
        Completed,
        NoShow
    }

    public class SalonService : Entity
    {

        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class Appointment : Entity
    {

        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty; // For terminology rule
        public Guid StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public AppointmentStatus Status { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<ProductUsage> UsedProducts { get; set; } = new();

        public bool ConflictsWith(Appointment other)
        {
            if (StaffId != other.StaffId) return false;
            if (Id == other.Id) return false;

            // Scenario A, B, C, D handled by standard interval overlap logic
            // (StartA < EndB) and (EndA > StartB)
            return StartTime < other.EndTime && EndTime > other.StartTime;
        }
    }

    public class ProductUsage : Entity
    {
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal Total => Quantity * PricePerUnit;
    }
}
