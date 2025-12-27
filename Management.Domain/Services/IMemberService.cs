using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Services
{
    public interface IMemberService
    {
        /// <summary>
        /// Retrieves a paginated list of members based on filter criteria.
        /// </summary>
        /// <returns>A PagedResult containing the list AND the total count.</returns>
        // FIX: Changed return type from List<MemberDto> to PagedResult<MemberDto>
        Task<PagedResult<MemberDto>> SearchMembersAsync(MemberSearchRequest request, int page = 1, int pageSize = 20);

        /// <summary>
        /// Gets the full profile details for a specific member.
        /// </summary>
        Task<MemberDto> GetMemberAsync(Guid id);

        /// <summary>
        /// Creates a new member and returns the generated ID.
        /// </summary>
        Task<Guid> CreateMemberAsync(MemberDto member);

        /// <summary>
        /// Updates an existing member's profile information.
        /// </summary>
        Task UpdateMemberAsync(MemberDto member);

        /// <summary>
        /// Soft-deletes a list of members.
        /// </summary>
        Task DeleteMembersAsync(List<Guid> ids);

        /// <summary>
        /// Extends the expiration date for a list of members based on their plan default.
        /// </summary>
        Task RenewMembersAsync(List<Guid> ids);

        // --- Dashboard Counters ---

        Task<int> GetActiveMemberCountAsync();
        Task<int> GetExpiringMemberCountAsync();
    }
}