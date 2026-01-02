using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

namespace Management.Application.Services
{
    public interface IMembershipPlanService
    {
        /// <summary>
        /// Retrieves all configured membership plans (Active and Archived).
        /// </summary>
        Task<Result<List<MembershipPlanDto>>> GetAllPlansAsync();

        /// <summary>
        /// Creates a new pricing tier.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.ValidationException">Thrown if price is negative or name empty.</exception>
        Task<Result> CreatePlanAsync(MembershipPlanDto plan);

        /// <summary>
        /// Updates an existing plan (e.g. changing price).
        /// </summary>
        Task<Result> UpdatePlanAsync(MembershipPlanDto plan);

        /// <summary>
        /// Archives or Soft-Deletes a plan.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if plan has active members assigned.</exception>
        Task<Result> DeletePlanAsync(Guid id);
    }
}