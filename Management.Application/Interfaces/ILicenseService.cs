using System.Threading.Tasks;
using Management.Application.DTOs;

namespace Management.Application.Interfaces
{
    public interface ILicenseService
    {
        Task<LicenseCheckResult> ValidateLicenseAsync(string key, string hardwareId);
    }
}
