using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using BCryptNet = BCrypt.Net.BCrypt;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Staff.Commands.CreateStaff
{
    public class CreateStaffCommandHandler : IRequestHandler<CreateStaffCommand, Result<Guid>>
    {
        private readonly IStaffRepository _staffRepository;
        private readonly IAuthenticationService _authService;
        private readonly ILogger<CreateStaffCommandHandler> _logger;
        private readonly IServiceProvider _serviceProvider;

        public CreateStaffCommandHandler(
            IStaffRepository staffRepository, 
            IAuthenticationService authService, 
            ILogger<CreateStaffCommandHandler> logger,
            IServiceProvider serviceProvider)
        {
            _staffRepository = staffRepository;
            _authService = authService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<Result<Guid>> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Staff;

            var emailResult = Email.Create(dto.Email);
            if (emailResult.IsFailure) return Result.Failure<Guid>(emailResult.Error);

            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            if (phoneResult.IsFailure) return Result.Failure<Guid>(phoneResult.Error);

            // 1. Create Domain Model FIRST (local-first)
            var staffResult = StaffMember.Recruit(
                dto.TenantId,
                dto.FacilityId,
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.Role,
                dto.Salary,
                dto.PaymentDay);

            if (staffResult.IsFailure) return Result.Failure<Guid>(staffResult.Error);

            var staff = staffResult.Value;
            
            if (dto.AllowedModules != null)
            {
                staff.SetAllowedModules(dto.AllowedModules);
            }

            // Fix 5: Hash PIN for offline fallback
            if (!string.IsNullOrEmpty(dto.Password))
            {
                staff.SetPinCode(BCryptNet.HashPassword(dto.Password));
                _logger.LogInformation("[CreateStaff] PIN hashed and set for local fallback for {Email}", dto.Email);
            }

            if (dto.Permissions != null)
            {
                foreach (var p in dto.Permissions)
                {
                    staff.SetPermission(p.Name, p.IsGranted);
                }
            }

            // 2. Save to local database FIRST
            try 
            {
                await _staffRepository.AddAsync(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CreateStaff] Database save failed for staff {StaffName}", staff.FullName);
                return Result.Failure<Guid>(new Error("Staff.SaveError", "Failed to save staff member locally."));
            }

            // 3. Try to create Supabase Auth (best effort, non-blocking)
            if (!string.IsNullOrEmpty(dto.Password))
            {
                try
                {
                    // Check if we can reach Supabase
                    var connectionService = _serviceProvider.GetService(typeof(Management.Domain.Services.IConnectionService)) as Management.Domain.Services.IConnectionService;
                    bool canReachCloud = connectionService != null && await connectionService.CanReachSupabaseAsync();
                    
                    if (canReachCloud)
                    {
                        var authResult = await _authService.RegisterStaffAsync(dto.Email, dto.Password);
                        if (authResult.IsSuccess)
                        {
                            staff.MarkAuthCompleted(authResult.Value.ToString());
                            await _staffRepository.UpdateAsync(staff);
                            _logger.LogInformation("[CreateStaff] Cloud auth created successfully for {Email}", dto.Email);
                        }
                        else
                        {
                            // Auth failed, mark as pending for retry
                            staff.MarkAuthPending(dto.Email);
                            await _staffRepository.UpdateAsync(staff);
                            _logger.LogWarning("[CreateStaff] Cloud auth failed for {Email}, marked as pending", dto.Email);
                        }
                    }
                    else
                    {
                        // Offline, mark as pending
                        staff.MarkAuthPending(dto.Email);
                        await _staffRepository.UpdateAsync(staff);
                        _logger.LogInformation("[CreateStaff] Offline mode, auth marked as pending for {Email}", dto.Email);
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: staff is already saved locally
                    staff.MarkAuthPending(dto.Email);
                    await _staffRepository.UpdateAsync(staff);
                    _logger.LogWarning(ex, "[CreateStaff] Cloud auth error for {Email}, marked as pending", dto.Email);
                }
            }

            return Result.Success(staff.Id);
        }
    }
}
