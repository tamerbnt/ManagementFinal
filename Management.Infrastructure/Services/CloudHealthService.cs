using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Supabase;
using Supabase.Postgrest;
using Management.Infrastructure.Integrations.Supabase.Models;

namespace Management.Infrastructure.Services
{
    public enum HealthStatus
    {
        Connected,
        AuthError,
        Unreachable,
        HighLatency
    }

    public class HealthReport
    {
        public HealthStatus Status { get; set; }
        public long LatencyMs { get; set; }
        public string? Message { get; set; }
    }

    public class CloudHealthService
    {
        private readonly Supabase.Client _supabase;

        public CloudHealthService(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public async Task<HealthReport> CheckConnectionAsync()
        {
            var report = new HealthReport();
            var sw = Stopwatch.StartNew();

            try
            {
                // 1. Auth Check: Is session expired?
                if (_supabase.Auth.CurrentSession == null || _supabase.Auth.CurrentSession.ExpiresAt() < DateTime.UtcNow)
                {
                    report.Status = HealthStatus.AuthError;
                    report.Message = "No active or valid session.";
                    return report;
                }

                // 2. Ping: Attempt a light read (exact count from profiles)
                // Using Supabase.Postgrest.Constants.CountType
                await _supabase.From<SupabaseProfile>().Count(Supabase.Postgrest.Constants.CountType.Exact);
                sw.Stop();
                report.LatencyMs = sw.ElapsedMilliseconds;

                // 3. Latency Check
                if (report.LatencyMs > 1000)
                {
                    report.Status = HealthStatus.HighLatency;
                    report.Message = $"Response time high: {report.LatencyMs}ms";
                }
                else
                {
                    report.Status = HealthStatus.Connected;
                    report.Message = "Cloud Connection Healthy";
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                report.Status = HealthStatus.Unreachable;
                report.Message = $"Cloud unreachable: {ex.Message}";
            }

            return report;
        }
    }
}
