using System;
using Management.Application.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IMembershipPlanService
    {
        /// <summary>
        /// Retrieves all configured membership plans (Active and Archived).
        /// </summary>
        Task<Result<List<MembershipPlanDto>>> GetAllPlansAsync(Guid facilityId);

        /// <summary>
        /// Creates a new pricing tier.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.ValidationException">Thrown if price is negative or name empty.</exception>
        Task<Result> CreatePlanAsync(Guid facilityId, MembershipPlanDto plan);

        /// <summary>
        /// Updates an existing plan (e.g. changing price).
        /// </summary>
        Task<Result> UpdatePlanAsync(Guid facilityId, MembershipPlanDto plan);

        /// <summary>
        /// Archives or Soft-Deletes a plan.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if plan has active members assigned.</exception>
        Task<Result> DeletePlanAsync(Guid facilityId, Guid id);
    }
}
