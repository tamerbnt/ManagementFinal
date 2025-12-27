using System;
using System.Collections.Generic;
using Management.Domain.Enums; // Requires TrendDirection enum

namespace Management.Domain.DTOs
{
    public class FinancialMetricsDto
    {
        // Revenue Stats
        public decimal MonthlyRevenue { get; set; }
        public double RevenueGrowth { get; set; } // Percentage (e.g. 15.5)
        public TrendDirection Trend { get; set; } // Up, Down, Stable

        // KPIs
        public decimal MRR { get; set; } // Monthly Recurring Revenue
        public decimal ARPU { get; set; } // Average Revenue Per User
        public double ChurnRate { get; set; } // Percentage

        // Operational Stats
        public int NewMembers { get; set; }
        public int TotalMembers { get; set; }
        public double SuccessRate { get; set; } // Payment Success Rate

        // Visualization Data
        public List<ChartPointDto> RevenueSparkline { get; set; } = new List<ChartPointDto>();
    }

    // Shared DTO for charting
    public class ChartPointDto
    {
        public double X { get; set; } // Time axis (or index)
        public double Y { get; set; } // Value axis
    }
}

