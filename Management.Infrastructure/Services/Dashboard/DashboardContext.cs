using Management.Domain.Enums;
using System;

namespace Management.Infrastructure.Services.Dashboard
{
    public class DashboardContext
    {
        public Guid FacilityId { get; set; }
        public FacilityType FacilityType { get; set; }
        public DateTime LocalToday { get; set; }
        public DateTime UtcDayStart { get; set; }
        public DateTime UtcDayEnd { get; set; }
        public DateTime UtcMonthStart { get; set; }
        public DateTime UtcYesterdayStart { get; set; }
        public DateTime UtcYesterdayEnd { get; set; }
        public DateTime UtcNow { get; set; }
        
        // Helper to check facility type
        public bool IsSalon => FacilityType == FacilityType.Salon;
        public bool IsRestaurant => FacilityType == FacilityType.Restaurant;
        public bool IsGym => FacilityType == FacilityType.Gym;
    }
}
