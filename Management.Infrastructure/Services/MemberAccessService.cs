using System;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Notifications;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Management.Infrastructure.Services
{
    public class MemberAccessService : IMemberAccessService
    {
        private readonly IAccessEventService _accessEventService;
        private readonly IMemberService _memberService;
        private readonly IMediator _mediator;
        private readonly IHardwareTurnstileService _turnstileService;
        private readonly ILogger<MemberAccessService> _logger;

        public MemberAccessService(
            IAccessEventService accessEventService,
            IMemberService memberService,
            IMediator mediator,
            IHardwareTurnstileService turnstileService,
            ILogger<MemberAccessService> logger)
        {
            _accessEventService = accessEventService;
            _memberService = memberService;
            _mediator = mediator;
            _turnstileService = turnstileService;
            _logger = logger;
        }

        public async Task<Result<AccessEventDto>> ProcessAccessFlowAsync(string cardId, Guid facilityId, ScanDirection direction, string? transactionId = null)
        {
            try
            {
                _logger.LogInformation("Processing access flow for CardId: {CardId}, FacilityId: {FacilityId}", cardId, facilityId);

                // 1. Validate Access
                var validationResult = await _accessEventService.ValidateAccessRequestAsync(cardId, facilityId, direction, transactionId);

                // 2. Resolve Member Details for Notification UI
                MemberDto? member = null;
                var memberSearch = await _memberService.SearchMembersAsync(facilityId, new MemberSearchRequest(cardId));
                if (memberSearch.IsSuccess && memberSearch.Value.Items.Any())
                {
                    member = memberSearch.Value.Items.First();
                }

                bool gateOpened = false;
                Result<AccessEventDto> accessResult = validationResult;

                // 3. Hardware Trigger & Commit on Success
                bool isGranted = validationResult.IsSuccess && 
                                 (validationResult.Value.AccessStatus == AccessStatus.Granted.ToString() || 
                                  validationResult.Value.AccessStatus == AccessStatus.Warning.ToString());

                if (isGranted)
                {
                    _logger.LogInformation("Access granted for {CardId}. Triggering hardware turnstile...", cardId);
                    gateOpened = await _turnstileService.OpenGateAsync();

                    if (gateOpened)
                    {
                        _logger.LogInformation("Gate opened successfully. Committing access for {CardId}.", cardId);
                        accessResult = await _accessEventService.CommitAccessRequestAsync(cardId, facilityId, direction, transactionId);
                    }
                    else 
                    {
                        _logger.LogWarning("Hardware failed to open gate for {CardId}. Access aborted to prevent session loss.", cardId);
                        accessResult = Result.Failure<AccessEventDto>(new Error("Gate.HardwareFailure", "The turnstile hardware failed to respond. Access was not recorded."));
                    }
                }
                else 
                {
                    _logger.LogInformation("Access denied for {CardId}. Logging attempt.", cardId);
                    // Still commit for denials to record the attempt in history
                    accessResult = await _accessEventService.CommitAccessRequestAsync(cardId, facilityId, direction, transactionId);
                }

                // 4. Publish Notification (Triggers UI Popup & Sounds)
                // We use the validation status for the notification to show "Granted/Warning/Denied" correctly
                var notifyStatus = AccessStatus.Denied;
                if (validationResult.IsSuccess)
                {
                    Enum.TryParse<AccessStatus>(validationResult.Value.AccessStatus, out notifyStatus);
                }

                var accessGrantedForNotification = validationResult.IsSuccess && 
                                                 (notifyStatus == AccessStatus.Granted || notifyStatus == AccessStatus.Warning);

                await _mediator.Publish(new MemberScannedNotification(
                    facilityId,
                    member,
                    cardId,
                    accessGrantedForNotification && (isGranted && gateOpened), // Only consider "Granted" for UI if gate actually opened
                    notifyStatus,
                    accessResult.IsFailure ? accessResult.Error.Message : (validationResult.Value.IsAccessGranted ? null : validationResult.Value.FailureReason)
                ));

                return accessResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System error during access flow processing for {CardId}", cardId);
                return Result.Failure<AccessEventDto>(new Error("Access.SystemError", "An unexpected error occurred. Please try again."));
            }
        }
    }
}
