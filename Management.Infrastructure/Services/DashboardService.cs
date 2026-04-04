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

            var context = new Management.Infrastructure.Services.Dashboard.DashboardContext
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
            var utcStart = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
            var utcEnd = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();

            var sales = await _saleRepository.GetByDateRangeAsync(facilityId, utcStart, utcEnd);
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

        public async Task<List<int>> GetWeeklyMemberGrowthAsync(Guid facilityId, int year, int month)
        {
            var startOfMonth = new DateTime(year, month, 1);
            var utcStart = startOfMonth.ToUniversalTime();
            var utcEnd = startOfMonth.AddMonths(1).ToUniversalTime();

            var registrationDays = await _dbContext.Members
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.FacilityId == facilityId && 
                            m.CreatedAt >= utcStart && 
                            m.CreatedAt < utcEnd && 
                            !m.IsDeleted)
                .Select(m => m.CreatedAt)
                .ToListAsync();

            var counts = new List<int>();
            counts.Add(registrationDays.Count(d => d.ToLocalTime().Day >= 1 && d.ToLocalTime().Day <= 7));
            counts.Add(registrationDays.Count(d => d.ToLocalTime().Day >= 8 && d.ToLocalTime().Day <= 14));
            counts.Add(registrationDays.Count(d => d.ToLocalTime().Day >= 15 && d.ToLocalTime().Day <= 21));
            counts.Add(registrationDays.Count(d => d.ToLocalTime().Day >= 22));

            return counts;
        }

        public async Task<List<PlanRevenueDto>> GetRevenueByMenuItemAsync(Guid facilityId, DateTime start, DateTime end)
        {
            var utcStart = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
            var utcEnd = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();

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
                    Count = g.Sum(i => i.Quantity)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }

        public async Task<List<PlanRevenueDto>> GetRevenueByProductAsync(Guid facilityId, DateTime start, DateTime end)
        {
            var sales = await _saleRepository.GetByDateRangeAsync(facilityId, start, end);
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

        public async Task<List<DateTimePoint>> GetRevenueTrendAsync(Guid facilityId, DateTime monthStart, DateTime monthEnd)
        {
            var utcStart = monthStart.Kind == DateTimeKind.Utc ? monthStart : monthStart.ToUniversalTime();
            var utcEnd   = monthEnd.Kind   == DateTimeKind.Utc ? monthEnd   : monthEnd.ToUniversalTime();

            // FIX: Materialize raw UTC timestamps first, THEN group in-memory.
            // EF Core / PostgreSQL cannot translate .ToLocalTime() inside LINQ-to-SQL GroupBy.
            var rawSales = await _dbContext.Sales
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.FacilityId == facilityId &&
                            s.Timestamp >= utcStart && s.Timestamp < utcEnd)
                .Select(s => new { s.Timestamp, Amount = s.TotalAmount.Amount })
                .ToListAsync();

            var rawOrders = await _dbContext.RestaurantOrders
                .AsNoTracking()
                .IgnoreQueryFilters()  // Added for consistency with Sales query
                .Where(o => o.FacilityId == facilityId &&
                            o.CompletedAt >= utcStart && o.CompletedAt < utcEnd &&
                            (o.Status == Management.Domain.Models.Restaurant.OrderStatus.Completed ||
                             o.Status == Management.Domain.Models.Restaurant.OrderStatus.Paid))
                .Select(o => new { Date = o.CompletedAt!.Value, Amount = o.Subtotal + o.Tax })
                .ToListAsync();

            // Group in-memory using local time (safe — data is already materialized)
            var salesMap = rawSales
                .GroupBy(s => s.Timestamp.ToLocalTime().Date)
                .ToDictionary(g => g.Key, g => g.Sum(s => (double)s.Amount));

            var restaurantMap = rawOrders
                .GroupBy(o => o.Date.ToLocalTime().Date)
                .ToDictionary(g => g.Key, g => g.Sum(o => (double)o.Amount));

            // Build a dense point per calendar day (zero-padded for missing days)
            var trend = new List<DateTimePoint>();
            int totalDays = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

            for (int i = 0; i < totalDays; i++)
            {
                var date  = monthStart.Date.AddDays(i);
                double total = 0;
                if (salesMap.TryGetValue(date, out var saleTotal))  total += saleTotal;
                if (restaurantMap.TryGetValue(date, out var restTotal)) total += restTotal;
                trend.Add(new DateTimePoint(date, total));
            }

            return trend;
        }

        public async Task<List<DateTimePoint>> GetSalonOccupancyTrendAsync(Guid facilityId)
        {
             var today = DateTime.Today;
             var utcStart = today.ToUniversalTime();
             var utcEnd = today.AddDays(1).ToUniversalTime();

             var reservations = await _reservationRepository.GetByDateRangeAsync(utcStart, utcEnd, facilityId);
             var trend = new List<DateTimePoint>();

             for (int i = 8; i <= 22; i++)
             {
                 var timeSlot = today.AddHours(i);
                 var utcTimeSlot = timeSlot.ToUniversalTime();
                 int count = reservations.Count(a => a.StartTime <= utcTimeSlot && a.EndTime > utcTimeSlot && a.Status != "Cancelled");
                 trend.Add(new DateTimePoint(timeSlot, count));
             }
             return trend;
        }

        public async Task<List<DateTimePoint>> GetGymOccupancyTrendAsync(Guid facilityId, DateTime? date = null)
        {
            var targetDate = (date ?? DateTime.Today).Date;
            var utcStart = targetDate.ToUniversalTime();
            var utcEnd = targetDate.AddDays(1).ToUniversalTime();
 
            var events = await _accessEventRepository.GetByDateRangeAsync(facilityId, utcStart, utcEnd);
            var granted = events.Where(e => e.IsAccessGranted).ToList();
 
            var trend = new List<DateTimePoint>();
            for (int h = 0; h <= 23; h++)
            {
                var hourSlot = targetDate.AddHours(h);
                var utcHourSlot = hourSlot.ToUniversalTime();
                var utcHourEnd = utcHourSlot.AddHours(1);
 
                int count = granted.Count(e => e.Timestamp >= utcHourSlot && e.Timestamp < utcHourEnd);
                trend.Add(new DateTimePoint(hourSlot, count));
            }
            return trend;
        }
        
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

                int count = orders
                    .Where(o => o.CreatedAt >= utcHourSlot && o.CreatedAt < utcHourEnd)
                    .Sum(o => o.PartySize);
                
                trend.Add(new DateTimePoint(hourSlot, count));
            }
            return trend;
        }

        public async Task<List<StaffPerformanceDto>> GetStaffPerformanceAsync(Guid facilityId, DateTime start, DateTime end)
        {
            var completed = await _dbContext.Appointments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.FacilityId == facilityId && 
                            (a.TenantId == _tenantService.GetTenantId() || a.TenantId == Guid.Empty) &&
                            !a.IsDeleted &&
                            a.StartTime >= start && a.StartTime < end && 
                            a.Status == AppointmentStatus.Completed && 
                            a.StaffId != Guid.Empty)
                .Join(_dbContext.StaffMembers.AsNoTracking().IgnoreQueryFilters().Where(s => s.FacilityId == facilityId), 
                      a => a.StaffId, 
                      s => s.Id, 
                      (a, s) => new { StaffId = a.StaffId, StaffName = s.FullName, ServiceId = a.ServiceId })
                .ToListAsync();

            if (!completed.Any())
                return new List<StaffPerformanceDto>();

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
                            .FirstOrDefaultAsync(s => s.Id == item.ServiceId);
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
            if (_facilityContext.CurrentFacilityId == facilityId) return _facilityContext.CurrentFacility;
            
            try
            {
                var facility = await _dbContext.Facilities
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f => f.Id == facilityId);
                
                if (facility != null) return facility.Type;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[DashboardService] Failed to resolve facility type for {Id}", facilityId);
            }
            return Management.Domain.Enums.FacilityType.General;
        }

        public async Task<RevenueHistoryDto> GetRevenueHistoryAsync(Guid facilityId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = new RevenueHistoryDto();
            var facilityType = await GetFacilityTypeByIdAsync(facilityId);
            var isSalon = facilityType == Management.Domain.Enums.FacilityType.Salon;

            // Senior Refactor: Use LEFT JOIN logic (Select into a shape with nullable Member)
            // This ensures products bought by guests are included.
            var baseQuery = _dbContext.Sales
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.FacilityId == facilityId && !s.IsDeleted);

            if (startDate.HasValue) baseQuery = baseQuery.Where(s => s.Timestamp >= startDate.Value);
            if (endDate.HasValue) baseQuery = baseQuery.Where(s => s.Timestamp <= endDate.Value);

            var revenueData = await (from s in baseQuery
                                    join m in _dbContext.Members.AsNoTracking().IgnoreQueryFilters() on s.MemberId equals m.Id into sm
                                    from m in sm.DefaultIfEmpty()
                                    select new 
                                    { 
                                        Amount = s.TotalAmount.Amount, 
                                        s.CapturedLabel, 
                                        s.Category, 
                                        Gender = (Management.Domain.Enums.Gender?)m.Gender, 
                                        s.Timestamp 
                                    })
                                    .ToListAsync();

            result.AnalysisPeriod = GetPeriodLabel(startDate, endDate);
            
            // 1. Calculate Monthly Highlights (Always for current calendar month)
            var utcNow = DateTime.UtcNow;
            var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            
            var monthData = await (from s in _dbContext.Sales.AsNoTracking().IgnoreQueryFilters()
                                  where s.FacilityId == facilityId && !s.IsDeleted && s.Timestamp >= monthStart
                                  select new { Amount = s.TotalAmount.Amount, s.CapturedLabel, s.Category })
                                  .ToListAsync();

            result.BestPlanOfMonth = monthData
                .Where(s => s.Category == Management.Domain.Enums.SaleCategory.Membership || 
                            s.Category == Management.Domain.Enums.SaleCategory.WalkIn ||
                            (isSalon && s.Category == Management.Domain.Enums.SaleCategory.Service))
                .GroupBy(s => string.IsNullOrEmpty(s.CapturedLabel) ? "Unknown Plan" : s.CapturedLabel)
                .Select(g => new PlanRevenueDto { PlanName = g.Key, Revenue = g.Sum(s => s.Amount), Count = g.Count() })
                .OrderByDescending(x => x.Revenue)
                .FirstOrDefault() ?? new PlanRevenueDto { PlanName = "No plans sold yet" };

            result.BestProductOfMonth = monthData
                .Where(s => s.Category == Management.Domain.Enums.SaleCategory.Product)
                .GroupBy(s => string.IsNullOrEmpty(s.CapturedLabel) ? "Retail Product" : s.CapturedLabel)
                .Select(g => new PopularItemDto { ItemName = g.Key, Revenue = g.Sum(s => s.Amount), Quantity = g.Count() })
                .OrderByDescending(x => x.Revenue)
                .FirstOrDefault() ?? new PopularItemDto { ItemName = "No products sold yet" };

            // 2. Demographic Metadata (Filtered by Selected Period)
            result.GenderSplit = new GenderSplitDto
            {
                MaleRevenue = revenueData.Where(x => x.Gender == Management.Domain.Enums.Gender.Male).Sum(x => x.Amount),
                FemaleRevenue = revenueData.Where(x => x.Gender == Management.Domain.Enums.Gender.Female).Sum(x => x.Amount),
                MaleCount = revenueData.Where(x => x.Gender == Management.Domain.Enums.Gender.Male).Count(),
                FemaleCount = revenueData.Where(x => x.Gender == Management.Domain.Enums.Gender.Female).Count()
            };

            result.TopPlans = revenueData
                .Where(s => s.Category == Management.Domain.Enums.SaleCategory.Membership || 
                            s.Category == Management.Domain.Enums.SaleCategory.WalkIn ||
                            (isSalon && s.Category == Management.Domain.Enums.SaleCategory.Service))
                .GroupBy(s => string.IsNullOrEmpty(s.CapturedLabel) ? "Unknown Plan/Service" : s.CapturedLabel)
                .Select(g => new PlanRevenueDto
                {
                    PlanName = g.Key,
                    Revenue = g.Sum(s => s.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            result.TopProducts = revenueData
                .Where(s => s.Category == Management.Domain.Enums.SaleCategory.Product)
                .GroupBy(s => string.IsNullOrEmpty(s.CapturedLabel) ? "Retail Product" : s.CapturedLabel)
                .Select(g => new PopularItemDto
                {
                    ItemName = g.Key,
                    Revenue = g.Sum(s => s.Amount),
                    Quantity = g.Count()
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            // Total Days calculation for the analyzed period
            var minDate = revenueData.Any() ? revenueData.Min(x => x.Timestamp) : monthStart;
            var maxDate = revenueData.Any() ? revenueData.Max(x => x.Timestamp) : DateTime.UtcNow;
            result.TotalDaysAnalyzed = (int)Math.Max(1, (maxDate - minDate).TotalDays);

            // 3. Goal Pacing Engine (Trend Continuation)
            decimal totalHistorical = revenueData.Sum(x => x.Amount);
            decimal fixedMrr = revenueData.Where(x => x.Category == Management.Domain.Enums.SaleCategory.Membership).Sum(x => x.Amount);
            decimal variablePos = totalHistorical - fixedMrr;

            int projectionDays = result.TotalDaysAnalyzed;
            decimal dailyVariableVelocity = variablePos / projectionDays;
            decimal projectedAdditional = dailyVariableVelocity * projectionDays;
            decimal targetGoal = totalHistorical * 1.3m; // Base goal is 30% higher than historical for presentation

            result.Prediction = new RevenuePredictionDto
            {
                HistoricalRevenue = totalHistorical,
                PredictedAdditionalRevenue = projectedAdditional,
                TargetGoal = targetGoal > 0 ? targetGoal : 10000,
                Title = $"PROJECTION (NEXT {projectionDays} DAYS)",
                Subtext = $"Based on {dailyVariableVelocity:N0} DA daily average (excluding fixed memberships)"
            };

            return result;
        }

        public async Task<OccupancyHistoryDto> GetOccupancyHistoryAsync(Guid facilityId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = new OccupancyHistoryDto();
            var type = await GetFacilityTypeByIdAsync(facilityId);
            
            var hourlyCounts = new Dictionary<int, List<int>>();
            for (int i = 0; i < 24; i++) hourlyCounts[i] = new List<int>();

            DateTime? firstDataPoint = null;

            if (type == Management.Domain.Enums.FacilityType.Gym)
            {
                var query = _dbContext.AccessEvents.AsNoTracking().IgnoreQueryFilters().Where(e => e.FacilityId == facilityId && e.IsAccessGranted && !e.IsDeleted);
                if (startDate.HasValue) query = query.Where(e => e.Timestamp >= startDate.Value);
                if (endDate.HasValue) query = query.Where(e => e.Timestamp <= endDate.Value);

                var events = await query.Select(e => e.Timestamp).ToListAsync();
                if (events.Any()) firstDataPoint = events.Min();

                foreach (var ts in events)
                {
                    hourlyCounts[ts.ToLocalTime().Hour].Add(1);
                }
            }
            else if (type == Management.Domain.Enums.FacilityType.Salon)
            {
                var query = _dbContext.Reservations.AsNoTracking().IgnoreQueryFilters().Where(r => r.FacilityId == facilityId && r.Status != "Cancelled" && !r.IsDeleted);
                if (startDate.HasValue) query = query.Where(r => r.StartTime >= startDate.Value);
                if (endDate.HasValue) query = query.Where(r => r.StartTime <= endDate.Value);

                var reservations = await query.Select(r => new { r.StartTime, r.EndTime }).ToListAsync();
                if (reservations.Any()) firstDataPoint = reservations.Min(x => x.StartTime);

                foreach (var res in reservations)
                {
                    var start = res.StartTime.ToLocalTime();
                    var end = res.EndTime.ToLocalTime();
                    for (int h = start.Hour; h <= end.Hour && h < 24; h++)
                    {
                        hourlyCounts[h].Add(1);
                    }
                }
            }
            else if (type == Management.Domain.Enums.FacilityType.Restaurant)
            {
                var query = _dbContext.RestaurantOrders.AsNoTracking().IgnoreQueryFilters().Where(o => o.FacilityId == facilityId && !o.IsDeleted);
                if (startDate.HasValue) query = query.Where(o => o.CreatedAt >= startDate.Value);
                if (endDate.HasValue) query = query.Where(o => o.CreatedAt <= endDate.Value);

                var orders = await query.Select(o => new { o.CreatedAt, o.PartySize }).ToListAsync();
                if (orders.Any()) firstDataPoint = orders.Min(x => x.CreatedAt);

                foreach (var order in orders)
                {
                    hourlyCounts[order.CreatedAt.ToLocalTime().Hour].Add(order.PartySize);
                }
            }

            // Senior Refactor: Facility-Aware Total Days detection
            double totalDays = 1;
            if (firstDataPoint.HasValue)
            {
                var end = endDate ?? DateTime.UtcNow;
                totalDays = Math.Max(1, (end - firstDataPoint.Value).TotalDays);
            }
            
            result.TotalDaysAnalyzed = (int)Math.Max(1, totalDays);
            result.AnalysisPeriod = GetPeriodLabel(startDate, endDate);

            result.HourlyAverages = hourlyCounts
                .Select(kvp => new HourlyOccupancyDto
                {
                    Hour = kvp.Key,
                    AverageOccupancy = Math.Round(kvp.Value.Count / totalDays, 1)
                })
                .OrderBy(x => x.Hour)
                .ToList();

            result.PeakHour = result.HourlyAverages.OrderByDescending(x => x.AverageOccupancy).FirstOrDefault()?.Hour ?? 0;
            return result;
        }

        private string GetPeriodLabel(DateTime? start, DateTime? end)
        {
            if (!start.HasValue && !end.HasValue) return "Lifetime Analysis";
            if (start.HasValue && !end.HasValue) return $"Since {start.Value:MMM dd, yyyy}";
            if (!start.HasValue && end.HasValue) return $"Up to {end.Value:MMM dd, yyyy}";
            return $"{start.Value:MMM dd} - {end.Value:MMM dd, yyyy}";
        }
    }
}
