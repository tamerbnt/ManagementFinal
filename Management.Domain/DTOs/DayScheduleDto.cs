namespace Management.Domain.DTOs
{
    public class DayScheduleDto
    {
        public string Day { get; set; } // "Monday"
        public string Open { get; set; } // "06:00"
        public string Close { get; set; } // "22:00"
        public bool IsActive { get; set; }
    }
}