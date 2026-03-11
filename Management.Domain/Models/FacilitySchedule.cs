using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class FacilitySchedule : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        
        public int DayOfWeek { get; set; } // 0: Sunday ... 6: Saturday
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int RuleType { get; set; } // 0: Both, 1: MaleOnly, 2: FemaleOnly

        public FacilitySchedule() : base() { }

        public FacilitySchedule(Guid id, Guid tenantId, Guid facilityId, int dayOfWeek, TimeSpan startTime, TimeSpan endTime, int ruleType) 
            : base(id)
        {
            TenantId = tenantId;
            FacilityId = facilityId;
            DayOfWeek = dayOfWeek;
            StartTime = startTime;
            EndTime = endTime;
            RuleType = ruleType;
        }
    }
}
