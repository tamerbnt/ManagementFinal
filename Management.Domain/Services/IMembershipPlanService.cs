using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Services
{
    public interface IMembershipPlanService
    {
        /// <summary>
        /// Retrieves all configured membership plans (Active and Archived).
        /// </summary>
        Task<List<MembershipPlanDto>> GetAllPlansAsync();

        /// <summary>
        /// Creates a new pricing tier.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.ValidationException">Thrown if price is negative or name empty.</exception>
        Task CreatePlanAsync(MembershipPlanDto plan);

        /// <summary>
        /// Updates an existing plan (e.g. changing price).
        /// </summary>
        Task UpdatePlanAsync(MembershipPlanDto plan);

        /// <summary>
        /// Archives or Soft-Deletes a plan.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if plan has active members assigned.</exception>
        Task DeletePlanAsync(Guid id);
    }
}