using System;
using System.Collections.Generic;

namespace Management.Application.DTOs
{
    public class ReportingSnapshotDto
    {
        public DateTime Date { get; set; }
        public string FacilityName { get; set; } = string.Empty;

        // Financials
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalPayroll { get; set; }
        public decimal TotalCogs { get; set; }
        public decimal NetProfit { get; set; }

        // Activities (Mirrors History View List)
        public List<ReportActivityItemDto> Activities { get; set; } = new();

        // Payroll Details
        public List<ReportPayrollItemDto> PayrollDetails { get; set; } = new();
    }

    public class ReportActivityItemDto
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty; // e.g., "Sale", "Access", "Appointment"
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public bool IsSuccessful { get; set; }
    }

    public class ReportPayrollItemDto
    {
        public string StaffName { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
        public string Status { get; set; } = string.Empty; // e.g., "Paid", "Pending"
    }
}
