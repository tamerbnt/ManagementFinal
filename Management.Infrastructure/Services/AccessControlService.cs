using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Services
{
    public class AccessControlService : IAccessControlService
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IStaffRepository _staffRepository;
        private readonly IRepository<MembershipPlan> _planRepository;
        private readonly IRepository<FacilitySchedule> _scheduleRepository;
        private readonly IFacilityContextService _facilityService;
        private readonly IAccessControlCache _cache;
        private readonly AppDbContext _context;
        private readonly ILogger<AccessControlService> _logger;

        public AccessControlService(
            IMemberRepository memberRepository,
            IStaffRepository staffRepository,
            IRepository<MembershipPlan> planRepository,
            IRepository<FacilitySchedule> scheduleRepository,
            IFacilityContextService facilityService,
            IAccessControlCache cache,
            AppDbContext context,
            ILogger<AccessControlService> logger)
        {
            _memberRepository = memberRepository;
            _staffRepository = staffRepository;
            _planRepository = planRepository;
            _scheduleRepository = scheduleRepository;
            _facilityService = facilityService;
            _cache = cache;
            _context = context;
            _logger = logger;
        }

        public async Task<ScanResult> ProcessScanAsync(string barcode, string? transactionId = null)
        {
            // 0. Persistent De-duplication (Hardware & Network retries)
            if (!string.IsNullOrEmpty(transactionId))
            {
                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
                var isDuplicate = await _context.AccessEvents
                    .AnyAsync(e => e.TransactionId == transactionId && e.Timestamp >= fiveMinutesAgo);

                if (isDuplicate)
                {
                    _logger.LogInformation("Rejected duplicate scan: TransactionId {Id} already processed in the last 5 minutes.", transactionId);
                    // Return OK but skip processing (session deduction happened on first scan)
                    return ScanResult.Granted("Access Restored (Duplicate)", null);
                }
            }
            else 
            {
                // Fallback de-duplication for UI-triggered scans without TransactionId (prevent double-clicks)
                var tenSecondsAgo = DateTime.UtcNow.AddSeconds(-5);
                var recentScan = await _context.AccessEvents
                    .AnyAsync(e => e.CardId == barcode && e.IsAccessGranted && e.Timestamp >= tenSecondsAgo);
                
                if (recentScan)
                {
                    _logger.LogInformation("Rejected duplicate scan: CardId {Id} processed too recently (manual scan).", barcode);
                    return ScanResult.Granted("Access Restored (Rapid Scan)", null);
                }
            }

            // 0. Staff Override (Staff ignore all behavioral rules)
            var staff = await _staffRepository.GetByCardIdAsync(barcode);
            if (staff != null)
            {
                if (!staff.IsActive)
                    return ScanResult.Denied("Staff Inactive", null);

                return ScanResult.Granted($"Staff: {staff.FullName}", null);
            }

            // 1. Identity
            var member = await _memberRepository.GetByCardIdAsync(barcode);
            if (member == null)
            {
                return ScanResult.Denied("Unknown ID");
            }

            // 2. Subscription Status
            if (!member.IsActive)
            {
                if (member.Status == MemberStatus.Banned)
                    return ScanResult.Denied("Member Banned", member);
                
                return ScanResult.Denied("Membership Expired", member);
            }

            // [QA CHECK] Facility Context & Shared Access
            var currentFacilityId = _facilityService.CurrentFacilityId;
            var currentFacilityType = _facilityService.CurrentFacility;
            
            // Get Plan for detailed checks
            MembershipPlan? plan = null;
            if (member.MembershipPlanId.HasValue)
            {
                plan = await _planRepository.GetByIdAsync(member.MembershipPlanId.Value);
            }

            // 3. Cross-Facility Validation
            if (plan != null)
            {
                // ALLOW if:
                // A. The plan explicitely lists this facility
                // B. The plan belongs to this facility (Home Gym Rule)
                var isHomeFacility = plan.FacilityId == currentFacilityId;
                var isRoamingAllowed = plan.AccessibleFacilities.Any(f => f.Id == currentFacilityId);

                if (!isHomeFacility && !isRoamingAllowed)
                {
                    return ScanResult.Denied("Plan not valid for this Facility", member);
                }
            }
            else if (currentFacilityType != FacilityType.General)
            {
                return ScanResult.Denied("Premium Access Required", member);
            }

            // 4. Behavioral Access Rules (Gender & Scheduling)
            
            // A. Gender Rule (Plan Level)
            if (plan != null && plan.GenderRule != 0) // 0: Both
            {
                var memberGenderInt = (int)member.Gender; // Assuming Enum mapping matches
                // Rule 1: MaleOnly (1), Rule 2: FemaleOnly (2)
                if (plan.GenderRule == 1 && member.Gender != Gender.Male)
                    return ScanResult.Denied("Plan is Male-Only", member);
                if (plan.GenderRule == 2 && member.Gender != Gender.Female)
                    return ScanResult.Denied("Plan is Female-Only", member);
            }

            // B. Scheduling (Facility Level)
            var facilityWindows = await GetFacilitySchedulesAsync(currentFacilityId);
            var (facilityOk, _, facReason) = ScheduleValidator.ValidateAccess(facilityWindows, DateTime.Now);
            if (!facilityOk)
            {
                return ScanResult.Denied(facReason ?? "Facility is Closed", member);
            }

            // C. Scheduling (Plan Level)
            if (plan != null && !string.IsNullOrEmpty(plan.ScheduleJson))
            {
                var planWindows = GetPlanSchedules(plan);
                var (planOk, _, planReason) = ScheduleValidator.ValidateAccess(planWindows, DateTime.Now);
                if (!planOk)
                {
                    return ScanResult.Denied(planReason ?? "Plan Outside Active Hours", member);
                }
            }

            // 5. Session Deduction
            if (plan != null && plan.IsSessionPack)
            {
                // ATOMIC — no race condition possible
                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                    @"UPDATE members 
                      SET remaining_sessions = remaining_sessions - 1, 
                          updated_at = datetime('now'), 
                          row_version = row_version + 1 
                      WHERE id = {0} 
                        AND remaining_sessions > 0 
                        AND facility_id = {1}",
                    member.Id, currentFacilityId);

                if (rowsAffected == 0)
                {
                    // Sessions ran out between our check and the update
                    return ScanResult.Denied("No Sessions Left", member);
                }
            }

            // 6. Grant with Warning
            var daysLeft = (member.ExpirationDate - DateTime.UtcNow).TotalDays;
            if (daysLeft < 3 && daysLeft > 0)
            {
                return ScanResult.Warning($"Expires in {Math.Ceiling(daysLeft)} days", member);
            }

            return ScanResult.Granted("Welcome", member);
        }

        private async Task<List<ScheduleWindow>> GetFacilitySchedulesAsync(Guid facilityId)
        {
            var cached = _cache.GetFacilitySchedules();
            if (cached.Any()) return cached;

            // Load from DB (Lazy Init)
            var schedules = await _scheduleRepository.GetAllAsync();
            var windows = schedules.Select(s => new ScheduleWindow
            {
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                RuleType = s.RuleType
            }).ToList();

            _cache.UpdateFacilitySchedules(windows);
            return windows;
        }

        private List<ScheduleWindow> GetPlanSchedules(MembershipPlan plan)
        {
            var cached = _cache.GetPlanSchedule(plan.Id);
            if (cached.Any()) return cached;

            _cache.UpdatePlanSchedule(plan.Id, plan.ScheduleJson);
            return _cache.GetPlanSchedule(plan.Id);
        }
    }
}
