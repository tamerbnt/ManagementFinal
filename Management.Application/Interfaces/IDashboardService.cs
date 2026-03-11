using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetSummaryAsync(Guid? facilityId = null);
        Task<List<PlanRevenueDto>> GetRevenueByPlanAsync(Guid facilityId, DateTime start, DateTime end);
        Task<List<PlanRevenueDto>> GetRevenueByProductAsync(Guid facilityId, DateTime start, DateTime end);
        Task<List<StaffPerformanceDto>> GetStaffPerformanceAsync(Guid facilityId, DateTime start, DateTime end);
        Task<List<LiveChartsCore.Defaults.DateTimePoint>> GetSalonOccupancyTrendAsync(Guid facilityId);
        Task<List<LiveChartsCore.Defaults.DateTimePoint>> GetGymOccupancyTrendAsync(Guid facilityId);
        Task<List<PlanRevenueDto>> GetRevenueByMenuItemAsync(Guid facilityId, DateTime start, DateTime end);
    }
}
