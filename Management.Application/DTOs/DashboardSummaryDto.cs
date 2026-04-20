using System;
using System.Collections.Generic;
using LiveChartsCore.Defaults;

namespace Management.Application.DTOs
{
    public class KpiMetricDto
    {
        public decimal Value { get; set; }
        public decimal ComparisonValue { get; set; }
        public decimal PercentChange { get; set; }
        public string PeriodLabel { get; set; } = string.Empty; // e.g., "MO", "7D", "LIVE"
        public string ComparisonLabel { get; set; } = string.Empty; // e.g., "vs last month"
        public bool IsIncreasePositive { get; set; } = true;
    }

    public class DashboardSummaryDto
    {
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int ExpiringSoonCount { get; set; }
        public int PendingRegistrationsCount { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyMembershipRevenue { get; set; }
        public decimal MonthlyMerchandiseRevenue { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal DailyRevenue { get; set; }
        public decimal DailyExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal DailyRevenueTarget { get; set; }
        public int ActiveMembersYesterday { get; set; }
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

        // Marketing & Business Intelligence (Gym Specific)
        public KpiMetricDto ChurnRate { get; set; } = new();
        public KpiMetricDto AvgVisitsPerWeek { get; set; } = new();
        public KpiMetricDto PeakCapacityPercent { get; set; } = new();
        public KpiMetricDto PtUpsellRate { get; set; } = new();
        public KpiMetricDto MembershipFreezeRate { get; set; } = new();
        public KpiMetricDto TrialConversionRate { get; set; } = new();
        public KpiMetricDto ClassFillRate { get; set; } = new();

        public List<int> VisitFrequencyDistribution { get; set; } = new();
        public List<PlanRevenueDto> MembershipBreakdown { get; set; } = new();
        public List<MemberGrowthDto> GrowthTrend { get; set; } = new();
        public List<GymClassPerformanceDto> ClassPerformance { get; set; } = new();

        // Salon BI (Aggregated)
        public KpiMetricDto SalonRebookingRate { get; set; } = new();
        public KpiMetricDto SalonRetailAttachRate { get; set; } = new();
        public KpiMetricDto SalonChairUtilization { get; set; } = new();
        public KpiMetricDto SalonAvgTicketValue { get; set; } = new();
        public List<SalonServiceProfitabilityDto> ServiceProfitability { get; set; } = new();
        public List<SalonStaffPerformanceDto> SalonStaffPerformance { get; set; } = new();
        public List<MemberSourceDto> MemberDemographics { get; set; } = new();
        public List<MemberSourceDto> GenderDemographics { get; set; } = new();
        public List<MemberSourceDto> AgeDemographics { get; set; } = new();
        public List<DemographicBucketDto> CombinedDemographics { get; set; } = new();
    }

    public class SalonServiceProfitabilityDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public double AvgDurationMinutes { get; set; }
        public decimal ProfitabilityIndex => AvgDurationMinutes > 0 ? Revenue / (decimal)AvgDurationMinutes : 0;
    }

    public class SalonStaffPerformanceDto
    {
        public string StaffName { get; set; } = string.Empty;
        public decimal RebookingRate { get; set; }
        public decimal AvgTicketValue { get; set; }
    }

    public class MemberSourceDto
    {
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Percentage { get; set; }
    }

    public class DemographicBucketDto
    {
        public string AgeGroup { get; set; } = string.Empty;
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
    }

    public class MemberGrowthDto
    {
        public string Month { get; set; } = string.Empty;
        public int NewMembers { get; set; }
        public int LostMembers { get; set; }
    }

    public class GymClassPerformanceDto
    {
        public string ClassName { get; set; } = string.Empty;
        public int Attendance { get; set; }
        public int Capacity { get; set; }
        public double FillRate => Capacity > 0 ? (double)Attendance / Capacity * 100 : 0;
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
