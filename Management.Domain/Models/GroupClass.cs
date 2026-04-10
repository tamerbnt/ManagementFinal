using System;
using System.Collections.Generic;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a scheduled group activity (e.g., Zumba, CrossFit WOD).
    /// </summary>
    public class GroupClass : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public string Name { get; private set; } = string.Empty;
        public string InstructorName { get; private set; } = string.Empty;
        public DateTime StartTime { get; private set; }
        public int DurationMinutes { get; private set; }
        public int MaxCapacity { get; private set; }
        public string ColorHex { get; private set; } = "#8257e5"; // Default purple

        private GroupClass(
            Guid id,
            string name,
            string instructorName,
            DateTime startTime,
            int durationMinutes,
            int maxCapacity) : base(id)
        {
            Name = name;
            InstructorName = instructorName;
            StartTime = startTime;
            DurationMinutes = durationMinutes;
            MaxCapacity = maxCapacity;
        }

        public GroupClass() { }

        public static GroupClass Create(
            string name,
            string instructorName,
            DateTime startTime,
            int durationMinutes,
            int maxCapacity)
        {
            return new GroupClass(Guid.NewGuid(), name, instructorName, startTime, durationMinutes, maxCapacity);
        }

        public void UpdateDetails(string name, string instructor, DateTime start, int duration, int capacity)
        {
            Name = name;
            InstructorName = instructor;
            StartTime = start;
            DurationMinutes = duration;
            MaxCapacity = capacity;
            UpdateTimestamp();
        }
    }
}
