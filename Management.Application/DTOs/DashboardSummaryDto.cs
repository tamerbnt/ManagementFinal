using System;
using System.Collections.Generic;
using LiveChartsCore.Defaults;

namespace Management.Application.DTOs
{
    public class DashboardSummaryDto
    {
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int ExpiringSoonCount { get; set; }
        public int PendingRegistrationsCount { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal DailyRevenue { get; set; }
        public decimal DailyExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal DailyRevenueTarget { get; set; }
        public int CheckInsToday { get; set; }
        public int OccupancyPercent { get; set; }
        public List<DateTimePoint> RevenueTrend { get; set; } = new();
        public List<DateTimePoint> MemberTrend { get; set; } = new();
        public List<ActivityItemDto> Activities { get; set; } = new();
        public List<ChurnRiskDto> ChurnRisks { get; set; } = new();
        public List<TransactionDto> RecentTransactions { get; set; } = new();
        public List<StaffPerformanceDto> TopPerformingStaff { get; set; } = new();
        public List<PopularItemDto> PopularItems { get; set; } = new();
        
        // Restaurant Specific
        public int ActiveTablesCount { get; set; }
        public int TotalTablesCount { get; set; }
        public int PendingOrdersCount { get; set; }
        public int TodayCovers { get; set; }
        public decimal AverageOrderValue { get; set; }

        // Salon-specific fields
        public int TodayAppointmentsTotal { get; set; }
        public int TodayAppointmentsCompleted { get; set; }
        public int TodayAppointmentsPending { get; set; }
        public int ActiveClientsThisMonth { get; set; }

        // Performance Trends
        public decimal RevenuePercentChange { get; set; }
        public decimal ExpensesPercentChange { get; set; }
        public decimal NetProfitPercentChange { get; set; }

        /// <summary>
        /// Number of people inside (check-ins) one hour ago. -1 means not yet calculated.
        /// </summary>
        public int PeopleInsideLastHour { get; set; } = -1;
    }

    public class ActivityItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // e.g., "CheckIn", "Revenue", "Alert"
        public string Icon { get; set; } = string.Empty;
    }
}
