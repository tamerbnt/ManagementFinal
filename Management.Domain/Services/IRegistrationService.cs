using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Services
{
    public interface IRegistrationService
    {
        /// <summary>
        /// Retrieves a paginated list of registrations based on search terms and filter type (New/Priority).
        /// Used by the Filter Panel in the Registrations View.
        /// </summary>
        /// <param name="request">Filter criteria (SearchText, FilterType).</param>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Items per page.</param>
        Task<PagedResult<RegistrationDto>> SearchAsync(RegistrationSearchRequest request, int page = 1, int pageSize = 50);

        /// <summary>
        /// Retrieves all currently pending registrations for the Inbox view.
        /// Optimized for the initial load of the "Action Required" list.
        /// </summary>
        Task<List<RegistrationDto>> GetPendingRegistrationsAsync();

        /// <summary>
        /// Retrieves full details for a specific registration entry.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.EntityNotFoundException">Thrown if ID is invalid.</exception>
        Task<RegistrationDto> GetRegistrationAsync(Guid id);

        /// <summary>
        /// Converts a pending registration into a full Member entity.
        /// </summary>
        /// <param name="id">The Registration ID.</param>
        /// <returns>The ID of the newly created Member.</returns>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if already processed.</exception>
        Task<Guid> ApproveRegistrationAsync(Guid id);

        /// <summary>
        /// Rejects a registration and marks it as declined (Soft Delete or Archived).
        /// </summary>
        Task DeclineRegistrationAsync(Guid id);

        // --- Bulk Operations ---

        /// <summary>
        /// Approves multiple registrations simultaneously. Transactional operation.
        /// </summary>
        Task ApproveBatchAsync(List<Guid> ids);

        /// <summary>
        /// Declines multiple registrations simultaneously.
        /// </summary>
        Task DeclineBatchAsync(List<Guid> ids);
    }
}