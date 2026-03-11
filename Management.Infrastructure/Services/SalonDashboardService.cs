using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Domain.Models;

namespace Management.Infrastructure.Services
{
    public class SalonDashboardService : ISalonDashboardService
    {
        private readonly ITenantService _tenantService;
        private readonly IServiceScopeFactory _scopeFactory;

        public SalonDashboardService(
            ITenantService tenantService,
            IServiceScopeFactory scopeFactory)
        {
            _tenantService = tenantService;
            _scopeFactory = scopeFactory;
        }

        public async Task<SalonDashboardDto> GetDashboardStatsAsync(Guid facilityId)
        {
            using var scope = _scopeFactory.CreateScope();
            var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
            var saleRepository = scope.ServiceProvider.GetRequiredService<ISaleRepository>();
            var staffRepository = scope.ServiceProvider.GetRequiredService<IStaffRepository>();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Fetch today's reservations (Scoped)
            var reservations = await reservationRepository.GetByDateRangeAsync(today, tomorrow, facilityId);
            
            // Fetch today's revenue (Scoped & Optimized)
            var totalRevenue = await saleRepository.GetTotalRevenueAsync(facilityId, today, tomorrow);

            // Fetch staff count (Scoped)
            var staffMembers = await staffRepository.GetAllActiveAsync(facilityId);
            var activeStaffCount = staffMembers.Count();

            // Calculate utilization: (Appts / (Staff * 8 slots per day))
            double totalPossibleSlots = Math.Max(1, activeStaffCount * 8);
            double utilization = (reservations.Count() / totalPossibleSlots) * 100;

            return new SalonDashboardDto
            {
                AppointmentsToday = reservations.Count(),
                TotalRevenue = totalRevenue,
                ChairUtilization = Math.Min(100, utilization),
                RebookingRate = 0 // Feature pending analytics implementation
            };
        }
    }
}
