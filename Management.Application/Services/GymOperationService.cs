using System;
using System.Threading.Tasks;
using System.Linq; // Added for Linq Select/Where
using Management.Application.DTOs;
using Management.Application.Interfaces; // For ICurrentUserService
using Management.Application.Interfaces.App;
using Management.Domain.Enums; // Added for AccessResult, SaleStatus, PaymentMethod
using Management.Domain.Models; // Added for Sale, SaleItem
using Management.Domain.Services;
using Management.Domain.Interfaces; // Added for Repositories
using MediatR;
using Management.Application.Notifications;
using Management.Domain.ValueObjects;
using Management.Application.Features.Turnstiles.Commands.LogAccessEvent;

namespace Management.Application.Services
{
    public class GymOperationService : IGymOperationService
    {
        private readonly IAccessControlService _accessService;
        private readonly IMediator _mediator;
        private readonly IAccessEventRepository _accessRepo;
        private readonly ISaleRepository _saleRepo;
        private readonly IMembershipPlanRepository _planRepo;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantService _tenantService;

        public GymOperationService(
            IAccessControlService accessService, 
            IMediator mediator,
            IAccessEventRepository accessRepo,
            ISaleRepository saleRepo,
            IMembershipPlanRepository planRepo,
            ICurrentUserService currentUserService,
            ITenantService tenantService)
        {
            _accessService = accessService;
            _mediator = mediator;
            _accessRepo = accessRepo;
            _saleRepo = saleRepo;
            _planRepo = planRepo;
            _currentUserService = currentUserService;
            _tenantService = tenantService;
        }

        public async Task<ScanResult> ProcessScanAsync(string input, Guid facilityId)
        {
            // Delegate to domain access service
            var result = await _accessService.ProcessScanAsync(input);
            
            // Log the access event to the database for the History tab
            bool granted = result.Status == AccessResult.Granted || result.Status == AccessResult.Warning;
            await _mediator.Send(new LogAccessEventCommand(
                FacilityId: facilityId,
                TurnstileId: Guid.Empty, // UI scan - no physical device
                CardId: input,
                TransactionId: $"UI-{DateTime.UtcNow.Ticks}",
                Granted: granted,
                Status: result.Status.ToString(),
                Direction: ScanDirection.Enter,
                Reason: result.Message
            ));

            return result;
        }

        public async Task<WalkInResult> ProcessWalkInAsync(decimal amount, Guid facilityId, string planName = "Walk-In")
        {
            // 1. Create Sale Record - Categorized as WalkIn
            var label = string.IsNullOrWhiteSpace(planName) ? "Walk-In" : planName;
            var saleResult = Sale.Create(null, PaymentMethod.Cash, "Walk-In", SaleCategory.WalkIn, label);
            if (!saleResult.IsSuccess)
            {
                return new WalkInResult { Success = false, Message = "Failed to create sale." };
            }

            var sale = saleResult.Value;
            sale.FacilityId = facilityId;
            sale.TenantId = _tenantService.GetTenantId() ?? Guid.Empty;
            
            // Add a default item
            var itemResult = SaleItem.Create(sale.Id, Guid.Empty, "Walk-In Pass", new Money(amount, "DA"), 1);
            if (itemResult.IsSuccess)
            {
                sale.AddItem(itemResult.Value);
            }

            await _saleRepo.AddAsync(sale);

            // 2. Log Access Event for occupancy tracking
            await _mediator.Send(new LogAccessEventCommand(
                FacilityId: facilityId,
                TurnstileId: Guid.Empty, // UI walk-in
                CardId: "WALK-IN",
                TransactionId: $"WI-{sale.Id}",
                Granted: true,
                Status: "Granted",
                Direction: ScanDirection.Enter,
                Reason: "Walk-In Entry"
            ));

            var result = new WalkInResult
            {
                Success = true,
                Message = "Walk-In Entry Granted",
                Amount = amount,
                ReceiptNumber = $"WI-{DateTime.Now:yyyyMMdd}-{sale.Id.ToString().Substring(0,4).ToUpper()}"
            };

            await _mediator.Publish(new FacilityActionCompletedNotification(
                facilityId,
                "Walk-In", 
                "Walk-In Guest", 
                result.Message,
                sale.Id.ToString()));

            return result;
        }

        public async Task<DailyStatsDto> GetDailyStatsAsync(Guid facilityId)
        {
            var nowUtc = DateTime.UtcNow;
            
            // Calculate start of today in LOCAL time, then convert to UTC for DB query
            // This ensures sales at 00:40 AM local are included correctly.
            var localTodayStart = DateTime.Today; // 00:00:00 Local
            var todayUtcStart = localTodayStart.ToUniversalTime();

            // Current live occupancy
            var occupancy = await _accessRepo.GetCurrentOccupancyCountAsync(facilityId);

            // Revenue from start of local day (in UTC) to end of local day (UTC)
            // Align with FinancialAggregator: use end of day instead of 'now' to avoid precision/race issues
            var localDayEnd = localTodayStart.AddDays(1);
            var todayUtcEnd = localDayEnd.ToUniversalTime();
            var revenue = await _saleRepo.GetTotalRevenueAsync(facilityId, todayUtcStart, todayUtcEnd);

            // Estimate occupancy one hour ago:
            // Query the events in the window [now-2h, now-1h] and count net check-ins
            var oneHourAgo = nowUtc.AddHours(-1);
            var twoHoursAgo = nowUtc.AddHours(-2);
            var lastHourEvents = await _accessRepo.GetByDateRangeAsync(facilityId, twoHoursAgo, oneHourAgo);
            var occupancyLastHour = lastHourEvents.Count(e => e.IsAccessGranted);

            return new DailyStatsDto
            {
                OccupancyCount = occupancy,
                DailyCashTotal = revenue,
                TotalVisitorsToday = occupancy,
                OccupancyLastHour = occupancyLastHour
            };
        }


        public async Task<bool> SellItemAsync(string? memberId, decimal amount, string productName, Guid facilityId, string? transactionType = null, SaleCategory category = SaleCategory.General, string capturedLabel = "")
        {
             var saleResult = Sale.Create(
                 memberId != null ? Guid.Parse(memberId) : null, 
                 PaymentMethod.Cash, 
                 transactionType ?? "QuickSale",
                 category,
                 string.IsNullOrEmpty(capturedLabel) ? productName : capturedLabel);

            if (!saleResult.IsSuccess) return false;

            var sale = saleResult.Value;
            sale.FacilityId = facilityId;
            sale.TenantId = _tenantService.GetTenantId() ?? Guid.Empty;
            
            var itemResult = SaleItem.Create(sale.Id, Guid.Empty, productName, new Money(amount, "DA"), 1);
            if (itemResult.IsSuccess)
            {
                sale.AddItem(itemResult.Value);
            }

            await _saleRepo.AddAsync(sale);

            await _mediator.Publish(new FacilityActionCompletedNotification(
                facilityId,
                "QuickSale", 
                productName, 
                $"Sold {productName} for {amount:N0} DA",
                sale.Id.ToString()));

            return true;
        }

        public async Task<System.Collections.Generic.IEnumerable<WalkInPlanDto>> GetWalkInPlansAsync(Guid facilityId)
        {
            var plans = await _planRepo.GetActivePlansAsync(facilityId);
            
            return plans
                .Where(p => p.IsActive && p.DurationDays <= 7) // Filter for short-term plans
                .Select(p => new WalkInPlanDto 
                { 
                    Name = p.Name, 
                    Price = p.Price.Amount, 
                    DurationDescription = $"{p.DurationDays} Days", 
                    Status = "Active" 
                });
        }
    }
}
