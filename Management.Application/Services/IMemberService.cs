using System;
using Management.Application.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IMemberService
    {
        /// <summary>
        /// Retrieves a paginated list of members based on filter criteria.
        /// </summary>
        /// <returns>A PagedResult containing the list AND the total count.</returns>
        // FIX: Changed return type from List<MemberDto> to PagedResult<MemberDto>
        Task<Result<PagedResult<MemberDto>>> SearchMembersAsync(Guid facilityId, MemberSearchRequest request, int page = 1, int pageSize = 20);

        /// <summary>
        /// Gets the full profile details for a specific member.
        /// </summary>
        Task<Result<MemberDto>> GetMemberAsync(Guid facilityId, Guid id);

        /// <summary>
        /// Creates a new member and returns the generated ID.
        /// </summary>
        Task<Result<Guid>> CreateMemberAsync(Guid facilityId, MemberDto member);

        /// <summary>
        /// Updates an existing member's profile information.
        /// </summary>
        Task<Result> UpdateMemberAsync(Guid facilityId, MemberDto member);

        /// <summary>
        /// Soft-deletes a list of members.
        /// </summary>
        Task<Result> DeleteMembersAsync(Guid facilityId, List<Guid> ids);

        /// <summary>
        /// Restores a list of soft-deleted members.
        /// </summary>
        Task<Result> RestoreMembersAsync(Guid facilityId, List<Guid> ids);

        /// <summary>
        /// Extends the expiration date for a list of members based on their plan default.
        /// </summary>
        Task<Result> RenewMembersAsync(Guid facilityId, List<Guid> ids);

        // --- Dashboard Counters ---

        Task<Result<int>> GetActiveMemberCountAsync(Guid facilityId);
        Task<Result<int>> GetExpiringMemberCountAsync(Guid facilityId);
    }
}
