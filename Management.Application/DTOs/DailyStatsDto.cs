namespace Management.Application.DTOs
{
    public class DailyStatsDto
    {
        public int OccupancyCount { get; set; }
        public decimal DailyCashTotal { get; set; }
        public int TotalVisitorsToday { get; set; }

        /// <summary>
        /// Number of people inside one hour ago. Used to compute a real trend delta.
        /// </summary>
        public int OccupancyLastHour { get; set; }
    }
}
