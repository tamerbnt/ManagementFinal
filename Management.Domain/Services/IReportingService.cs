using System;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Primitives;

namespace Management.Domain.Services
{
    public interface IReportingService
    {
        /// <summary>
        /// Calculates revenue forecast for next month using weighted moving average.
        /// </summary>
        Task<Result<decimal>> GetRevenueForecastAsync(Guid facilityId);

        /// <summary>
        /// Generates a professional PDF report for daily close with Sequoia styling.
        /// </summary>
        Task<Result<byte[]>> GenerateDailyClosePDFAsync(Guid facilityId, DateTime date);
    }
}
