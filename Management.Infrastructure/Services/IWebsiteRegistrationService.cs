using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Infrastructure.Integrations.Supabase.Models;

namespace Management.Infrastructure.Services
{
    public interface IWebsiteRegistrationService
    {
        Task<IEnumerable<SupabaseRegistrationRequest>> FetchPendingRequestsAsync(string facilitySlug, string? pcGender = null);
        Task UpdateRequestStatusAsync(string requestId, string status);
    }
}
