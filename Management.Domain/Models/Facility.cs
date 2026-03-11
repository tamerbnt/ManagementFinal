using System;
using System.Collections.Generic;
using Management.Domain.Common;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using ITenantEntity = Management.Domain.Primitives.ITenantEntity;

namespace Management.Domain.Models
{
    public class Facility : BaseEntity, ITenantEntity
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FacilityType Type { get; set; } = FacilityType.General;
        public bool IsActive { get; set; } = true;

        public virtual ICollection<MembershipPlan> AccessiblePlans { get; set; } = new List<MembershipPlan>();
        public virtual ICollection<FacilitySchedule> Schedules { get; set; } = new List<FacilitySchedule>();

        public Facility() : base() { }
        public Facility(Guid id) : base(id) { }
    }
}
