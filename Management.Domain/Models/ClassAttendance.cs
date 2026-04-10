using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    /// <summary>
    /// Tracks a member's attendance at a specific group class.
    /// </summary>
    public class ClassAttendance : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public Guid GroupClassId { get; private set; }
        public Guid MemberId { get; private set; }
        public DateTime CheckInTime { get; private set; }

        private ClassAttendance(Guid id, Guid groupClassId, Guid memberId) : base(id)
        {
            GroupClassId = groupClassId;
            MemberId = memberId;
            CheckInTime = DateTime.UtcNow;
        }

        public ClassAttendance() { }

        public static ClassAttendance Record(Guid groupClassId, Guid memberId)
        {
            return new ClassAttendance(Guid.NewGuid(), groupClassId, memberId);
        }
    }
}
