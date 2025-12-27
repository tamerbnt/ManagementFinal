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
    public class MemberService : IMemberService
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMembershipPlanRepository _planRepository;
        private readonly MemberStore _memberStore;

        public MemberService(
            IMemberRepository memberRepository,
            IMembershipPlanRepository planRepository,
            MemberStore memberStore)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
            _memberStore = memberStore;
        }

        public async Task<PagedResult<MemberDto>> SearchMembersAsync(MemberSearchRequest request, int page = 1, int pageSize = 20)
        {
            // 1. Fetch filtered list from Repository
            // Note: In a high-scale app, pagination should happen at the SQL level (Repository).
            // For V1, we get the filtered set and page it here.
            var allMatches = await _memberRepository.SearchAsync(request.SearchTerm,
                request.FilterType == MemberFilterType.All ? null : MapFilterToStatus(request.FilterType));

            // 2. Filter logic for "Expiring" (Business Logic)
            if (request.FilterType == MemberFilterType.Expiring)
            {
                var threshold = DateTime.UtcNow.AddDays(7);
                allMatches = allMatches.Where(m => m.Status == MemberStatus.Active && m.ExpirationDate <= threshold);
            }

            var totalCount = allMatches.Count();

            // 3. Apply Pagination
            var pagedEntities = allMatches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 4. Map to DTOs
            var dtos = new List<MemberDto>();
            foreach (var entity in pagedEntities)
            {
                dtos.Add(await MapToDto(entity));
            }

            return new PagedResult<MemberDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<MemberDto> GetMemberAsync(Guid id)
        {
            var entity = await _memberRepository.GetByIdAsync(id);
            if (entity == null) throw new EntityNotFoundException(nameof(Member), id);
            return await MapToDto(entity);
        }

        public async Task<Guid> CreateMemberAsync(MemberDto dto)
        {
            // 1. Validation (Basic)
            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new ValidationException(new Dictionary<string, string[]> { { "FullName", new[] { "Name is required." } } });

            // 2. Map to Entity
            var entity = new Member
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                CardId = dto.CardId,
                Status = MemberStatus.Active, // Default to Active on create
                StartDate = dto.StartDate,
                ExpirationDate = dto.ExpirationDate,
                ProfileImageUrl = dto.ProfileImageUrl,
                EmergencyContactName = dto.EmergencyContactName,
                EmergencyContactPhone = dto.EmergencyContactPhone,
                Notes = dto.Notes
            };

            // Link Plan if provided
            // In a real app, we'd look up the Plan ID based on the PlanName from the DTO, 
            // but the DTO usually carries the selected PlanId from the dropdown.
            // For now, we assume logic elsewhere handled ID resolution or we skip linking if ID is missing.

            // 3. Persist
            await _memberRepository.AddAsync(entity);

            // 4. Update DTO with new ID and Broadcast
            dto.Id = entity.Id;
            dto.Status = MemberStatus.Active;

            _memberStore.TriggerMemberAdded(dto);

            return entity.Id;
        }

        public async Task UpdateMemberAsync(MemberDto dto)
        {
            var entity = await _memberRepository.GetByIdAsync(dto.Id);

            // Map updates
            entity.FullName = dto.FullName;
            entity.Email = dto.Email;
            entity.PhoneNumber = dto.PhoneNumber;
            entity.CardId = dto.CardId;
            entity.ProfileImageUrl = dto.ProfileImageUrl;
            entity.Notes = dto.Notes;
            // Status and Dates usually updated via specific workflows (Renew/Cancel), not generic update

            await _memberRepository.UpdateAsync(entity);

            // Broadcast
            _memberStore.TriggerMemberUpdated(dto);
        }

        public async Task DeleteMembersAsync(List<Guid> ids)
        {
            foreach (var id in ids)
            {
                await _memberRepository.DeleteAsync(id);
                _memberStore.TriggerMemberDeleted(id);
            }
        }

        public async Task RenewMembersAsync(List<Guid> ids)
        {
            foreach (var id in ids)
            {
                var member = await _memberRepository.GetByIdAsync(id);

                // Logic: Find their plan to see how many months to add
                int monthsToAdd = 1; // Default
                if (member.MembershipPlanId.HasValue)
                {
                    var plan = await _planRepository.GetByIdAsync(member.MembershipPlanId.Value);
                    monthsToAdd = plan.DurationMonths;
                }

                // If currently expired, start from Today. If active, append to current expiry.
                if (member.ExpirationDate < DateTime.UtcNow)
                {
                    member.ExpirationDate = DateTime.UtcNow.AddMonths(monthsToAdd);
                }
                else
                {
                    member.ExpirationDate = member.ExpirationDate.AddMonths(monthsToAdd);
                }

                member.Status = MemberStatus.Active;

                await _memberRepository.UpdateAsync(member);

                // Broadcast
                _memberStore.TriggerMemberUpdated(await MapToDto(member));
            }
        }

        public async Task<int> GetActiveMemberCountAsync()
        {
            return await _memberRepository.GetActiveCountAsync();
        }

        public async Task<int> GetExpiringMemberCountAsync()
        {
            return await _memberRepository.GetExpiringCountAsync(DateTime.UtcNow.AddDays(7));
        }

        // --- Helpers ---

        private async Task<MemberDto> MapToDto(Member entity)
        {
            string planName = "None";
            if (entity.MembershipPlanId.HasValue)
            {
                try
                {
                    var plan = await _planRepository.GetByIdAsync(entity.MembershipPlanId.Value);
                    planName = plan.Name;
                }
                catch
                {
                    planName = "Unknown/Deleted";
                }
            }

            return new MemberDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
                CardId = entity.CardId,
                Status = entity.Status,
                StartDate = entity.StartDate,
                ExpirationDate = entity.ExpirationDate,
                ProfileImageUrl = entity.ProfileImageUrl,
                MembershipPlanName = planName,
                EmergencyContactName = entity.EmergencyContactName,
                EmergencyContactPhone = entity.EmergencyContactPhone,
                Notes = entity.Notes
            };
        }

        private MemberStatus? MapFilterToStatus(MemberFilterType filter)
        {
            return filter switch
            {
                MemberFilterType.Active => MemberStatus.Active,
                MemberFilterType.Expired => MemberStatus.Expired,
                // 'Expiring' is a logic check on Active members, not a status enum
                MemberFilterType.Expiring => MemberStatus.Active,
                _ => null
            };
        }
    }
}