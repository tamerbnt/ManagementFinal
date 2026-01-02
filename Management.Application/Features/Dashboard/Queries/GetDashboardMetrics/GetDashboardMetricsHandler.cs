using Management.Application.Features.Dashboard.Queries.GetDashboardMetrics;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Services;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Dashboard.Queries.GetDashboardMetrics
{
    public class GetDashboardMetricsHandler : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
    {
        private readonly IMemberService _memberService;
        private readonly IRegistrationService _registrationService;
        private readonly IAccessEventService _accessEventService;
        private readonly IFacilityContextService _facilityContext;

        public GetDashboardMetricsHandler(
            IMemberService memberService,
            IRegistrationService registrationService,
            IAccessEventService accessEventService,
            IFacilityContextService facilityContext)
        {
            _memberService = memberService;
            _registrationService = registrationService;
            _accessEventService = accessEventService;
            _facilityContext = facilityContext;
        }

        public async Task<DashboardMetricsDto> Handle(GetDashboardMetricsQuery request, CancellationToken cancellationToken)
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var facilityType = _facilityContext.CurrentFacility;

            // 1. Parallel Fetch (Architecture Requirement: Async Flow Purity)
            var t1 = _memberService.GetActiveMemberCountAsync(facilityId);
            var t2 = _registrationService.GetPendingRegistrationsAsync();
            var t3 = _accessEventService.GetRecentEventsAsync(facilityId, 10);
            var t4 = _accessEventService.GetCurrentOccupancyAsync(facilityId);

            await Task.WhenAll(t1, t2, t3, t4);

            var activeMemberCount = (await t1).Value;
            var registrations = (await t2).Value;
            var accessEvents = (await t3).Value;
            var occupancy = (await t4).Value;

            // 2. Aggregate into DTO
            var dto = new DashboardMetricsDto
            {
                TotalActiveMembers = activeMemberCount,
                PendingRegistrationsCount = registrations.Count,
                RecentRegistrations = registrations.Take(5).ToList(),
                ActivityFeed = accessEvents,
                ActivePeopleCount = occupancy
            };

            // 3. Facility Specific Logic (Moved from ViewModel)
            if (facilityType == FacilityType.Restaurant)
            {
                // In a real implementation, these would also come from services
                // For now, we mimic the ViewModel's previous mock logic but in the Application layer
                return dto with 
                { 
                    ActiveOrdersCount = 12,
                    TodayRevenue = 2450.50m,
                    OccupancyPercentage = 65.5
                };
            }

            return dto;
        }
    }
}
