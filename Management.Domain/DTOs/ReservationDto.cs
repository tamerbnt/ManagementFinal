using System;

namespace Management.Domain.DTOs
{
    public class ReservationDto
    {
        public Guid Id { get; set; }
        public string ActivityName { get; set; }
        public string InstructorName { get; set; }
        public string Location { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}