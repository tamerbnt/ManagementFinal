using System.Collections.Generic;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class DashboardMetricsDto
    {
        public decimal MonthlyRevenue { get; set; }
        public double RevenueGrowth { get; set; }
        public TrendDirection Trend { get; set; }

        public decimal MRR { get; set; }
        public decimal ARPU { get; set; }
        public double ChurnRate { get; set; }
        public double SuccessRate { get; set; }

        public int NewMembers { get; set; }
        public int TotalMembers { get; set; }

        public List<SparklinePointDto> RevenueSparkline { get; set; } = new List<SparklinePointDto>();
    }

    public class SparklinePointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}