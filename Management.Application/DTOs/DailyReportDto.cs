using System;
using System.Collections.Generic;

namespace Management.Application.DTOs
{
    public class DailyReportDto
    {
        public string FacilityName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime ReportDate { get; set; }

        // Financial Summary
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal RevenuePercentChange { get; set; }
        public decimal ExpensesPercentChange { get; set; }
        public decimal NetProfitPercentChange { get; set; }

        // Operations
        public int TotalAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CheckIns { get; set; }

        // Detailed Data
        public List<StaffPerformanceDto> TopStaff { get; set; } = new();
        public List<TransactionDto> MajorTransactions { get; set; } = new();
    }
}
