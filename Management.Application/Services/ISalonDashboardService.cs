using System.Threading.Tasks;

namespace Management.Application.Services
{
    public class SalonDashboardDto
    {
        public int AppointmentsToday { get; set; }
        public decimal TotalRevenue { get; set; }
        public double ChairUtilization { get; set; }
        public double RebookingRate { get; set; }
    }

    public interface ISalonDashboardService
    {
        Task<SalonDashboardDto> GetDashboardStatsAsync(Guid facilityId);
    }
}
