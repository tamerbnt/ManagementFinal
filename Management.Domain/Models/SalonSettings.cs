using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class SalonSettings : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public int TotalChairs { get; set; } = 1;
        public decimal DailyRevenueTarget { get; set; } = 1000m;
        public string OperatingHoursJson { get; set; } = "{}";
    }
}
