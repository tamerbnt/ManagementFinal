using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Domain.Interfaces
{
    public interface ITurnstileRepository : IRepository<Turnstile>
    {
        Task<Turnstile?> GetByHardwareIdAsync(string hardwareId, System.Guid? facilityId = null);
        Task<Turnstile?> GetByIdAsync(System.Guid id, System.Guid? facilityId = null);
    }
}
