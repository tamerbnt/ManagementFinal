using System;

namespace Management.Domain.Models
{
    public class ScheduleWindow
    {
        public int DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int RuleType { get; set; } // 0: Both, 1: MaleOnly, 2: FemaleOnly

        public bool IsActive(DateTime currentLocalTime)
        {
            if ((int)currentLocalTime.DayOfWeek != DayOfWeek) return false;

            var timeOfDay = currentLocalTime.TimeOfDay;

            // Handle midnight span (e.g., 22:00 to 04:00)
            if (StartTime < EndTime)
            {
                return timeOfDay >= StartTime && timeOfDay <= EndTime;
            }
            else
            {
                // Start is after End, meaning it crosses midnight
                return timeOfDay >= StartTime || timeOfDay <= EndTime;
            }
        }
    }
}
