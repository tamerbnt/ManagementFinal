using System.Collections.Generic;
using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record FinancialMetricsDto(
        decimal MonthlyRevenue,
        double RevenueGrowth,
        TrendDirection Trend,
        decimal MRR,
        decimal ARPU,
        double ChurnRate,
        int NewMembers,
        int TotalMembers,
        double SuccessRate,
        List<ChartPointDto> RevenueSparkline
    );

    public record ChartPointDto(double X, double Y);
}

