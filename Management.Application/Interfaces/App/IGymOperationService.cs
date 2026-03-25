using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Models;

namespace Management.Application.Interfaces.App
{
    public interface IGymOperationService
    {
        /// <summary>
        /// Processes a scan input (HID scanner or manual typing).
        /// Identifies if it's a Member ID, Name lookup, or Command.
        /// </summary>
        Task<ScanResult> ProcessScanAsync(string input, System.Guid facilityId);

        /// <summary>
        /// Handles a walk-in guest entry with cash collection.
        /// </summary>
        Task<WalkInResult> ProcessWalkInAsync(decimal amount, System.Guid facilityId, string planName = "Walk-In", bool publishNotification = true);

        /// <summary>
        /// Retrieves daily operational statistics.
        /// </summary>
        Task<DailyStatsDto> GetDailyStatsAsync(Guid facilityId);

        /// <summary>
        /// Retrieves available walk-in plans.
        /// </summary>
        Task<System.Collections.Generic.IEnumerable<WalkInPlanDto>> GetWalkInPlansAsync(Guid facilityId);

        /// <summary>
        /// Sells a product (attached to member or guest).
        /// </summary>
        Task<bool> SellItemAsync(string? memberId, decimal amount, string productName, Guid facilityId, string? transactionType = null, SaleCategory category = SaleCategory.General, string capturedLabel = "", bool publishNotification = true);
    }
}
