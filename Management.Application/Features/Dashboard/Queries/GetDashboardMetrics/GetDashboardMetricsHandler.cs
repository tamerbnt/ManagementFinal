using Management.Application.Features.Dashboard.Queries.GetDashboardMetrics;
using Management.Application.Services;
using Management.Application.Interfaces;
using Management.Application.DTOs;
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
        private readonly IOrderService _orderService;
        private readonly IInventoryService _inventoryService;

        public GetDashboardMetricsHandler(
            IMemberService memberService,
            IRegistrationService registrationService,
            IAccessEventService accessEventService,
            IFacilityContextService facilityContext,
            IOrderService orderService,
            IInventoryService inventoryService)
        {
            _memberService = memberService;
            _registrationService = registrationService;
            _accessEventService = accessEventService;
            _facilityContext = facilityContext;
            _orderService = orderService;
            _inventoryService = inventoryService;
        }

        public async Task<DashboardMetricsDto> Handle(GetDashboardMetricsQuery request, CancellationToken cancellationToken)
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var facilityType = _facilityContext.CurrentFacility;

            // 1. Parallel Fetch (Architecture Requirement: Async Flow Purity)
            var t1 = _memberService.GetActiveMemberCountAsync(facilityId);
            var t2 = _registrationService.GetPendingRegistrationsAsync(facilityId);
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

            if (facilityType == FacilityType.Restaurant)
            {
                var today = DateTime.Today; // Keep local date for Business Day comparisons

                // COGS/Expenses from Inventory (uses yyyy-MM-dd strings internally)
                var purchasesTask = _inventoryService.GetPurchasesByRangeAsync(facilityId, today, today);
                var revenueTask = _orderService.GetTodayRevenueAsync(facilityId, today.ToUniversalTime(), today.AddDays(1).ToUniversalTime());
                var activeOrdersTask = _orderService.GetActiveOrdersAsync(facilityId);

                await Task.WhenAll(purchasesTask, revenueTask, activeOrdersTask);

                var purchases = await purchasesTask;
                var revenueResult = await revenueTask;
                var activeOrdersResult = await activeOrdersTask;

                decimal todayExpenses = purchases.Sum(p => p.TotalPrice);
                decimal todayRevenue = revenueResult.IsSuccess ? revenueResult.Value : 0;
                int activeOrders = activeOrdersResult.IsSuccess ? activeOrdersResult.Value.Count() : 0;

                return dto with 
                { 
                    ActiveOrdersCount = activeOrders,
                    TodayRevenue = todayRevenue,
                    TodayExpenses = todayExpenses,
                    OccupancyPercentage = occupancy > 0 ? (double)occupancy / 100.0 : 0 // Example mapping
                };
            }

            return dto;
        }
    }
}