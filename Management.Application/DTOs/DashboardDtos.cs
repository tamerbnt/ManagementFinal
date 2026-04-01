using System;
using System.Collections.Generic;

namespace Management.Application.DTOs
{
    public class FinancialSummaryDto
    {
        public decimal NetProfit { get; set; }
        public decimal NetProfitPercentChange { get; set; }
        public decimal Revenue { get; set; }
        public decimal RevenuePercentChange { get; set; }
        public decimal Expenses { get; set; }
        public decimal ExpensesPercentChange { get; set; }
        public decimal MembershipsRevenue { get; set; }
        public decimal MerchandiseRevenue { get; set; }
        public decimal Salaries { get; set; }
        public decimal Rent { get; set; }
        public decimal Utilities { get; set; }
    }

    public class TransactionDto
    {
        public string MemberName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Membership Renewal", "PT Session", "Merchandise"
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = "Success"; // "Success", "Failed"
    }

    public class ChurnRiskDto
    {
        public string MemberName { get; set; } = string.Empty;
        public int DaysSinceLastVisit { get; set; }
        public string RiskLevel { get; set; } = "Low"; // "High", "Medium", "Low"
        public string Reason { get; set; } = string.Empty; // "Low Attendance", "Payment Failure"
    }

    public class StaffPerformanceDto
    {
        public string StaffName { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int AppointmentCount { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;
    }

    public class PlanRevenueDto
    {
        public string PlanName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; } // 0-100, share of total revenue
    }

    public class PopularItemDto
    {
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public double Percentage { get; set; }
    }

    public class RevenueHistoryDto
    {
        public List<PlanRevenueDto> TopPlans { get; set; } = new();
        public List<PopularItemDto> TopProducts { get; set; } = new();
        public GenderSplitDto GenderSplit { get; set; } = new();
        public PlanRevenueDto BestPlanOfMonth { get; set; } = new();
        public PopularItemDto BestProductOfMonth { get; set; } = new();
        public string AnalysisPeriod { get; set; } = string.Empty; // e.g. "Lifetime" or "Last 30 Days"
        public int TotalDaysAnalyzed { get; set; }
    }

    public class GenderSplitDto
    {
        public decimal MaleRevenue { get; set; }
        public decimal FemaleRevenue { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public double MalePercentage => (double)(MaleRevenue + FemaleRevenue == 0 ? 0 : MaleRevenue / (MaleRevenue + FemaleRevenue) * 100);
        public double FemalePercentage => 100 - MalePercentage;
    }

    public class OccupancyHistoryDto
    {
        public int PeakHour { get; set; }
        public string PeakHourFormatted => $"{PeakHour:D2}:00";
        public List<HourlyOccupancyDto> HourlyAverages { get; set; } = new();
        public List<OccupancyIntervalDto> BestIntervals { get; set; } = new();
        public string AnalysisPeriod { get; set; } = string.Empty;
        public int TotalDaysAnalyzed { get; set; }
    }

    public class HourlyOccupancyDto
    {
        public int Hour { get; set; }
        public double AverageOccupancy { get; set; }
    }

    public class OccupancyIntervalDto
    {
        public string Name { get; set; } = string.Empty; // e.g., "Morning Peak"
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public double MaxOccupancy { get; set; }
    }
}
