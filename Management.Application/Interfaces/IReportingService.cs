using System;
using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Interfaces
{
    public interface IReportingService
    {
        Task<ReportingSnapshotDto> GetDailySnapshotAsync(Guid facilityId, DateTime date);
        Task<byte[]> GenerateDailyPdfReportAsync(ReportingSnapshotDto snapshot);
        Task<byte[]> GenerateDailyExcelReportAsync(ReportingSnapshotDto snapshot);
        Task<byte[]> GenerateHistoryPdfReportAsync(string facilityName, DateTime selectedDay, System.Collections.Generic.IEnumerable<UnifiedHistoryEventDto> events);

        // Compatibility for Dashboard
        Task<DailyReportDto> GetDailyReportDataAsync(Guid facilityId, DateTime date);
        Task<string> GenerateDailyPdfReportAsync(DailyReportDto data);
        Task<byte[]> GenerateRevenueHistoryPdfAsync(RevenueHistoryDto data, string facilityName);
        Task<byte[]> GenerateOccupancyHistoryPdfAsync(OccupancyHistoryDto data, string facilityName);
    }
}
