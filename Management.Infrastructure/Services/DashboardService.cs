using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiveChartsCore.Defaults;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Interfaces;
using Management.Domain.Models.Salon;
using Management.Domain.Services;
using Management.Domain.Models.Restaurant;
using OrderStatus = Management.Domain.Models.Restaurant.OrderStatus;
using Microsoft.EntityFrameworkCore;
using Management.Infrastructure.Services.Dashboard;
using Microsoft.Extensions.Logging;

namespace Management.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IAccessEventRepository _accessEventRepository;
        private readonly IRegistrationRepository _registrationRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ITenantService _tenantService;
        private readonly IPayrollRepository _payrollRepository;
        private readonly IProductRepository _productRepository;
        private readonly IFacilityContextService _facilityContext;
        private readonly Management.Infrastructure.Data.AppDbContext _dbContext;
        private readonly ILogger<DashboardService> _logger;

        private readonly IEnumerable<IDashboardAggregator> _aggregators;

        public DashboardService(
            IMemberRepository memberRepository,
            IAccessEventRepository accessEventRepository,
            IRegistrationRepository registrationRepository,
            ISaleRepository saleRepository,
            IReservationRepository reservationRepository,
            IAppointmentRepository appointmentRepository,
            ITenantService tenantService,
            IPayrollRepository payrollRepository,
            IProductRepository productRepository,
            IFacilityContextService facilityContext,
            Management.Infrastructure.Data.AppDbContext dbContext,
            ILogger<DashboardService> logger,
            IEnumerable<IDashboardAggregator> aggregators)
        {
            _memberRepository = memberRepository;
            _accessEventRepository = accessEventRepository;
            _registrationRepository = registrationRepository;
            _saleRepository = saleRepository;
            _reservationRepository = reservationRepository;
            _appointmentRepository = appointmentRepository;
            _tenantService = tenantService;
            _payrollRepository = payrollRepository;
            _productRepository = productRepository;
            _facilityContext = facilityContext;
            _dbContext = dbContext;
            _logger = logger;
            _aggregators = aggregators;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(Guid? overrideFacilityId = null)
        {
            var facilityId = overrideFacilityId ?? _facilityContext.CurrentFacilityId;
            var localToday = DateTime.Now.Date;

            var context = new Dashboard.DashboardContext
            {
                FacilityId = facilityId,
                TenantId = _tenantService.GetTenantId(),
                FacilityType = _facilityContext.CurrentFacilityId == facilityId 
                    ? _facilityContext.CurrentFacility 
                    : (overrideFacilityId.HasValue ? await GetFacilityTypeByIdAsync(facilityId) : Management.Domain.Enums.FacilityType.General),
                LocalToday = localToday,
                UtcDayStart = localToday.ToUniversalTime(),
                UtcDayEnd = localToday.AddDays(1).ToUniversalTime(),
                UtcMonthStart = new DateTime(localToday.Year, localToday.Month, 1).ToUniversalTime(),
                UtcYesterdayStart = localToday.AddDays(-1).ToUniversalTime(),
                UtcYesterdayEnd = localToday.ToUniversalTime(),
                UtcNow = DateTime.UtcNow
            };

            var dto = new DashboardSummaryDto();

            // Run all applicable aggregators concurrently. Thread-safety is guaranteed because
            // each aggregator writes to a distinct, non-overlapping set of DTO properties.
            var tasks = _aggregators
                .Where(a => a.CanHandle(context))
                .Select(a => RunAggregatorSafeAsync(a, dto, context));

            await Task.WhenAll(tasks);

            return dto;
        }

        private async Task RunAggregatorSafeAsync(IDashboardAggregator aggregator, DashboardSummaryDto dto, DashboardContext context)
        {
            try
            {
                await aggregator.AggregateAsync(dto, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in dashboard aggregator {AggregatorName}", aggregator.GetType().Name);
            }
        }

        public async Task<List<PlanRevenueDto>> GetRevenueByPlanAsync(Guid facilityId, DateTime start, DateTime end)
        {
            // Fix: UI passes Local Time. Convert to UTC for DB query.
            var utcStart = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
            var utcEnd = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();

            var sales = await _saleRepository.GetByDateRangeAsync(facilityId, utcStart, utcEnd);
            
            // Category-based routing: the SaleCategory is set authoritatively at point-of-sale.
            // Membership = scheduled plan / POS plan purchase
            // WalkIn = walk-in entry
            // Service = salon appointment completion (included in "Plan" revenue for Salons)
            var isSalon = (await GetFacilityTypeByIdAsync(facilityId)) == Management.Domain.Enums.FacilityType.Salon;
            var planSales = sales
                .Where(s => s.Category == Management.Domain.Enums.SaleCategory.Membership || 
                            s.Category == Management.Domain.Enums.SaleCategory.WalkIn ||
                            (isSalon && s.Category == Management.Domain.Enums.SaleCategory.Service))
                .ToList();

            return planSales
                .GroupBy(s => string.IsNullOrEmpty(s.CapturedLabel) ? "Unknown Plan" : s.CapturedLabel)
                .Select(g => new PlanRevenueDto
                {
                    PlanName = g.Key,
                    Revenue = g.Sum(s => s.TotalAmount.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }

        public async Task<List<PlanRevenueDto>> GetRevenueByMenuItemAsync(Guid facilityId, DateTime start, DateTime end)
        {
            var utcStart = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
            var utcEnd = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();

            // Aggregating quantity and revenue per menu item from completed orders
            var orderItems = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .Where(o => o.FacilityId == facilityId && 
                            o.CompletedAt >= utcStart && o.CompletedAt <= utcEnd &&
                            (o.Status == Management.Domain.Models.Restaurant.OrderStatus.Completed || 
                             o.Status == Management.Domain.Models.Restaurant.OrderStatus.Paid))
                .SelectMany(o => o.Items)
                .Select(i => new { i.Name, i.Price, i.Quantity })
                .ToListAsync();

            return orderItems
                .GroupBy(i => i.Name)
                .Select(g => new PlanRevenueDto
                {
                    PlanName = g.Key,
                    Revenue = g.Sum(i => (decimal)i.Price * i.Quantity),
                    Count = g.Sum(i => i.Quantity) // Treat "Count" as number of items sold
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }

        public async Task<List<PlanRevenueDto>> GetRevenueByProductAsync(Guid facilityId, DateTime start, DateTime end)
        {
            var sales = await _saleRepository.GetByDateRangeAsync(facilityId, start, end);
            
            // Category-based routing: Product = retail checkout; Service = non-plan salon work
            var productSales = sales
                .Where(s => s.Category == Management.Domain.Enums.SaleCategory.Product || 
                            s.Category == Management.Domain.Enums.SaleCategory.Service)
                .ToList();

            return productSales
                .GroupBy(s => string.IsNullOrEmpty(s.CapturedLabel) ? "Miscellaneous Product" : s.CapturedLabel)
                .Select(g => new PlanRevenueDto
                {
                    PlanName = g.Key,
                    Revenue = g.Sum(s => s.TotalAmount.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }

        // NEW: Salon specific trend for Occupancy Chart (Active Appointments per Hour)
        public async Task<List<DateTimePoint>> GetSalonOccupancyTrendAsync(Guid facilityId)
        {
             var today = DateTime.Today;
             var utcStart = today.ToUniversalTime();
             var utcEnd = today.AddDays(1).ToUniversalTime();

             // Use Reservations
             var reservations = await _reservationRepository.GetByDateRangeAsync(utcStart, utcEnd, facilityId);
             var trend = new List<DateTimePoint>();

             // Calculate active reservations for each hour of the day (0-23)
             for (int i = 8; i <= 22; i++) // Shop hours 8am-10pm typically
             {
                 var timeSlot = today.AddHours(i);
                 var utcTimeSlot = timeSlot.ToUniversalTime();
                 // Count reservations that overlap with this hour
                 int count = reservations.Count(a => a.StartTime <= utcTimeSlot && a.EndTime > utcTimeSlot && a.Status != "Cancelled");
                 trend.Add(new DateTimePoint(timeSlot, count));
             }
             return trend;
        }

        // NEW: Gym hourly check-in trend for the Occupancy Chart (check-ins per hour for today)
        public async Task<List<DateTimePoint>> GetGymOccupancyTrendAsync(Guid facilityId)
        {
            var today = DateTime.Today;
            var utcStart = today.ToUniversalTime();
            var utcEnd = today.AddDays(1).ToUniversalTime();

            var events = await _accessEventRepository.GetByDateRangeAsync(facilityId, utcStart, utcEnd);
            var granted = events.Where(e => e.IsAccessGranted).ToList();

            var trend = new List<DateTimePoint>();
            for (int h = 0; h <= 23; h++)
            {
                var hourSlot = today.AddHours(h);
                var utcHourSlot = hourSlot.ToUniversalTime();
                var utcHourEnd = utcHourSlot.AddHours(1);
                int count = granted.Count(e => e.Timestamp >= utcHourSlot && e.Timestamp < utcHourEnd);
                trend.Add(new DateTimePoint(hourSlot, count));
            }
            return trend;
        }
        
        // NEW: Restaurant occupancy trend (Covers/PartySize per hour for today)
        public async Task<List<DateTimePoint>> GetRestaurantOccupancyTrendAsync(Guid facilityId)
        {
            var today = DateTime.Today;
            var utcStart = today.ToUniversalTime();
            var utcEnd = today.AddDays(1).ToUniversalTime();

            var orders = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .Where(o => o.FacilityId == facilityId && o.CreatedAt >= utcStart && o.CreatedAt < utcEnd)
                .ToListAsync();

            var trend = new List<DateTimePoint>();
            for (int h = 0; h <= 23; h++)
            {
                var hourSlot = today.AddHours(h);
                var utcHourSlot = hourSlot.ToUniversalTime();
                var utcHourEnd = utcHourSlot.AddHours(1);

                // For restaurants, "Occupancy" is sum of party sizes in orders created/active this hour
                int count = orders
                    .Where(o => o.CreatedAt >= utcHourSlot && o.CreatedAt < utcHourEnd)
                    .Sum(o => o.PartySize);
                
                trend.Add(new DateTimePoint(hourSlot, count));
            }
            return trend;
        }


        public async Task<List<StaffPerformanceDto>> GetStaffPerformanceAsync(Guid facilityId, DateTime start, DateTime end)
        {
            // Use Appointments for Staff Performance (confirmed as source of truth for Salon)
            // Senior Fix: We use the times as provided (Local for Salon, UTC for others).
            // We also add IgnoreQueryFilters() to the direct DB join to ensure data matches even if context is shifting.
            var completed = await _dbContext.Appointments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.FacilityId == facilityId && 
                            (a.TenantId == _tenantService.GetTenantId() || a.TenantId == Guid.Empty) &&
                            !a.IsDeleted &&
                            a.StartTime >= start && a.StartTime < end && 
                            a.Status == AppointmentStatus.Completed && 
                            a.StaffId != Guid.Empty)
                .Join(_dbContext.StaffMembers.AsNoTracking().IgnoreQueryFilters().Where(s => s.FacilityId == facilityId && (s.TenantId == _tenantService.GetTenantId() || s.TenantId == Guid.Empty)), 
                      a => a.StaffId, 
                      s => s.Id, 
                      (a, s) => new { StaffId = a.StaffId, StaffName = s.FullName, ServiceId = a.ServiceId })
                .ToListAsync();

            if (!completed.Any())
                return new List<StaffPerformanceDto>();

            // Calculate revenue from SalonServices price
            var staffPerformance = new List<StaffPerformanceDto>();
            var groupedByStaff = completed.GroupBy(x => x.StaffName);

            foreach (var group in groupedByStaff)
            {
                decimal totalRevenue = 0;
                foreach (var item in group)
                {
                    if (item.ServiceId != Guid.Empty)
                    {
                        var service = await _dbContext.SalonServices
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(s => s.Id == item.ServiceId && (s.TenantId == _tenantService.GetTenantId() || s.TenantId == Guid.Empty));
                        if (service != null) totalRevenue += service.BasePrice;
                    }
                }

                staffPerformance.Add(new StaffPerformanceDto
                {
                    StaffName = group.Key,
                    AppointmentCount = group.Count(),
                    TotalSales = totalRevenue
                });
            }

            return staffPerformance
                .OrderByDescending(s => s.TotalSales)
                .Take(5)
                .ToList();
        }

        private async Task<Management.Domain.Enums.FacilityType> GetFacilityTypeByIdAsync(Guid facilityId)
        {
            // Fallback for aggregators when override is used
            if (_facilityContext.CurrentFacilityId == facilityId) return _facilityContext.CurrentFacility;
            
            try
            {
                var facility = await _dbContext.Facilities
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f => f.Id == facilityId && (f.TenantId == _tenantService.GetTenantId() || f.TenantId == Guid.Empty));
                
                if (facility != null)
                {
                    return facility.Type;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[DashboardService] Failed to resolve facility type for {Id}", facilityId);
            }

            // Default to General if we can't be sure
            return Management.Domain.Enums.FacilityType.General;
        }
    }
}
