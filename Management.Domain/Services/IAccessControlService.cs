using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Services
{
    public interface IAccessControlService
    {
        Task<ScanResult> ValidateAccessAsync(string barcode, string? transactionId, Management.Domain.Enums.ScanDirection direction);
        Task<ScanResult> CommitAccessAsync(string barcode, System.Guid facilityId, Management.Domain.Enums.ScanDirection direction, string? transactionId);
    }
}
