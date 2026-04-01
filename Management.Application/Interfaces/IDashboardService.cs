using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetSummaryAsync(Guid? facilityId = null);
        Task<List<int>> GetWeeklyMemberGrowthAsync(Guid facilityId, int year, int month);
        Task<List<PlanRevenueDto>> GetRevenueByPlanAsync(Guid facilityId, DateTime start, DateTime end);

        Task<List<PlanRevenueDto>> GetRevenueByProductAsync(Guid facilityId, DateTime start, DateTime end);
        Task<List<StaffPerformanceDto>> GetStaffPerformanceAsync(Guid facilityId, DateTime start, DateTime end);
        Task<List<LiveChartsCore.Defaults.DateTimePoint>> GetSalonOccupancyTrendAsync(Guid facilityId);
        Task<List<LiveChartsCore.Defaults.DateTimePoint>> GetGymOccupancyTrendAsync(Guid facilityId, DateTime? date = null);
        Task<List<PlanRevenueDto>> GetRevenueByMenuItemAsync(Guid facilityId, DateTime start, DateTime end);
        Task<List<LiveChartsCore.Defaults.DateTimePoint>> GetRevenueTrendAsync(Guid facilityId, DateTime monthStart, DateTime monthEnd);
        Task<RevenueHistoryDto> GetRevenueHistoryAsync(Guid facilityId, DateTime? startDate = null, DateTime? endDate = null);
        Task<OccupancyHistoryDto> GetOccupancyHistoryAsync(Guid facilityId, DateTime? startDate = null, DateTime? endDate = null);
    }
}
