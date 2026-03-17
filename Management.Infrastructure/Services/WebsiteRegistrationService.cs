using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Infrastructure.Integrations.Supabase.Models;
using static Supabase.Postgrest.Constants;

namespace Management.Infrastructure.Services
{
    public class WebsiteRegistrationService : IWebsiteRegistrationService
    {
        private readonly Supabase.Client _supabase;

        public WebsiteRegistrationService(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public async Task<IEnumerable<SupabaseRegistrationRequest>> FetchPendingRequestsAsync(string facilitySlug, string? pcGender = null)
        {
            // Call 1: Primary facility requests
            var primaryTask = _supabase.From<SupabaseRegistrationRequest>()
                .Filter("facility_slug", Operator.Equals, facilitySlug)
                .Filter("status", Operator.Equals, "pending")
                .Get();

            // Call 2: Aqua overflow requests based on PC gender
            Task<Supabase.Postgrest.Responses.ModeledResponse<SupabaseRegistrationRequest>>? secondaryTask = null;
            if (facilitySlug != "aqua" && !string.IsNullOrEmpty(pcGender))
            {
                secondaryTask = _supabase.From<SupabaseRegistrationRequest>()
                    .Filter("facility_slug", Operator.Equals, "aqua")
                    .Filter("gender", Operator.Equals, pcGender)
                    .Filter("status", Operator.Equals, "pending")
                    .Get();
            }

            var primaryResponse = await primaryTask;
            var results = primaryResponse.Models.ToList();

            if (secondaryTask != null)
            {
                var secondaryResponse = await secondaryTask;
                foreach (var item in secondaryResponse.Models)
                {
                    if (!results.Any(r => r.Id == item.Id))
                    {
                        results.Add(item);
                    }
                }
            }

            return results.OrderByDescending(r => r.CreatedAt);
        }

        public async Task UpdateRequestStatusAsync(string requestId, string status)
        {
            await _supabase.From<SupabaseRegistrationRequest>()
                .Where(x => x.Id == requestId)
                .Set(x => x.Status, status)
                .Update();
        }
    }
}
