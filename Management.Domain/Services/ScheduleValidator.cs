using System;
using System.Collections.Generic;
using System.Linq;
using Management.Domain.Models;

namespace Management.Domain.Services
{
    public static class ScheduleValidator
    {
        private const int GracePeriodMinutes = 5;

        /// <summary>
        /// Checks if the current time allows access based on a set of schedule windows.
        /// Returns (IsAllowed, CurrentRuleType, Reason)
        /// </summary>
        public static (bool IsAllowed, int RuleType, string? Reason) ValidateAccess(
            IEnumerable<ScheduleWindow> windows, 
            DateTime currentTime)
        {
            if (windows == null || !windows.Any())
            {
                return (true, 0, null); // No windows = Unrestricted
            }

            // 1. Check for explicit active windows (including grace period)
            var activeWindows = windows.Where(w => w.IsActive(currentTime.AddMinutes(GracePeriodMinutes)) || 
                                                  w.IsActive(currentTime.RemoveMinutes(GracePeriodMinutes)));

            // Note: currentTime.AddMinutes(GracePeriodMinutes) handles the early entry.
            // Simplified logic: Check if current time is within any window +/- grace.
            
            bool found = false;
            int effectiveRule = 0;

            foreach (var window in windows)
            {
                if (IsWithinWindowWithGrace(window, currentTime))
                {
                    found = true;
                    effectiveRule = window.RuleType;
                    break; 
                }
            }

            if (!found)
            {
                return (false, 0, "No active schedule window at this time.");
            }

            return (true, effectiveRule, null);
        }

        private static bool IsWithinWindowWithGrace(ScheduleWindow window, DateTime current)
        {
            var startWithGrace = window.StartTime.Subtract(TimeSpan.FromMinutes(GracePeriodMinutes));
            var endWithGrace = window.EndTime.Add(TimeSpan.FromMinutes(GracePeriodMinutes));

            var time = current.TimeOfDay;

            if ((int)current.DayOfWeek != window.DayOfWeek) return false;

            if (window.StartTime < window.EndTime)
            {
                return time >= startWithGrace && time <= endWithGrace;
            }
            else
            {
                return time >= startWithGrace || time <= endWithGrace;
            }
        }
    }

    // Helper extension if needed
    public static class DateTimeExtensions
    {
        public static DateTime RemoveMinutes(this DateTime dt, int minutes) => dt.AddMinutes(-minutes);
    }
}
