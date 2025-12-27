using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Exceptions;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Application.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IMemberService _memberService; // Calls MemberService to ensure logic consistency
        private readonly RegistrationStore _registrationStore; // Syncs Dashboard Badge

        public RegistrationService(
            IRegistrationRepository registrationRepository,
            IMemberService memberService,
            RegistrationStore registrationStore)
        {
            _registrationRepository = registrationRepository;
            _memberService = memberService;
            _registrationStore = registrationStore;
        }

        public async Task<List<RegistrationDto>> GetPendingRegistrationsAsync()
        {
            var entities = await _registrationRepository.GetPendingAsync();
            return entities.Select(MapToDto).ToList();
        }

        public async Task<PagedResult<RegistrationDto>> SearchAsync(RegistrationSearchRequest request, int page = 1, int pageSize = 50)
        {
            // For V1, simplified search on Pending items
            // In real app, might search archived/declined leads too
            var allPending = await _registrationRepository.GetPendingAsync();

            var query = allPending.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(r => r.FullName.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (request.FilterType == RegistrationFilterType.New)
            {
                var threshold = DateTime.UtcNow.AddHours(-24);
                query = query.Where(r => r.CreatedAt >= threshold);
            }

            var list = query.ToList();
            return new PagedResult<RegistrationDto>
            {
                Items = list.Select(MapToDto),
                TotalCount = list.Count,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<Guid> ApproveRegistrationAsync(Guid id)
        {
            var registration = await _registrationRepository.GetByIdAsync(id);

            // 1. Create Member
            var memberDto = new MemberDto
            {
                FullName = registration.FullName,
                Email = registration.Email,
                PhoneNumber = registration.PhoneNumber,
                StartDate = registration.PreferredStartDate ?? DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddMonths(1), // Default 1 month if no plan selected
                Notes = registration.Notes,
                MembershipPlanName = "Standard" // Should lookup Plan Name by RequestedPlanId in Repo
            };

            var newMemberId = await _memberService.CreateMemberAsync(memberDto);

            // 2. Remove Registration (It is now a member)
            await _registrationRepository.DeleteAsync(id);

            // 3. Update UI
            _registrationStore.TriggerRegistrationProcessed(id);

            return newMemberId;
        }

        public async Task DeclineRegistrationAsync(Guid id)
        {
            await _registrationRepository.DeleteAsync(id);
            _registrationStore.TriggerRegistrationProcessed(id);
        }

        public async Task ApproveBatchAsync(List<Guid> ids)
        {
            foreach (var id in ids)
            {
                try { await ApproveRegistrationAsync(id); }
                catch { /* Log individual failure but continue batch */ }
            }
        }

        public async Task DeclineBatchAsync(List<Guid> ids)
        {
            foreach (var id in ids)
            {
                await DeclineRegistrationAsync(id);
            }
        }

        public async Task<RegistrationDto> GetRegistrationAsync(Guid id)
        {
            var entity = await _registrationRepository.GetByIdAsync(id);
            return MapToDto(entity);
        }

        private RegistrationDto MapToDto(Registration entity)
        {
            return new RegistrationDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
                Source = entity.Source,
                CreatedAt = entity.CreatedAt,
                Status = entity.Status,
                Notes = entity.Notes,
                PreferredStartDate = entity.PreferredStartDate
            };
        }
    }
}