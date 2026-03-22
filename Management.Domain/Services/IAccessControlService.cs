using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Services
{
    public interface IAccessControlService
    {
        Task<ScanResult> ProcessScanAsync(string barcode, string? transactionId = null);
    }
}
