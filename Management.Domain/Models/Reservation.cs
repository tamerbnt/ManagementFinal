using System;

namespace Management.Domain.Models
{
    public class Reservation : Entity
    {
        public Guid MemberId { get; set; }

        public string ActivityName { get; set; } // "Personal Training"
        public string InstructorName { get; set; }
        public string Location { get; set; } // "Zone A"

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool IsCancelled { get; set; }
    }
}