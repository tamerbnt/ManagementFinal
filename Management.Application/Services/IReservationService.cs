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
    public interface IReservationService
    {
        /// <summary>
        /// Retrieves bookings falling within the specified date range for the History/Schedule view.
        /// </summary>
        Task<Result<List<ReservationDto>>> GetReservationsByRangeAsync(DateTime start, DateTime end);

        /// <summary>
        /// Retrieves upcoming reservations for a specific member.
        /// </summary>
        Task<Result<List<ReservationDto>>> GetReservationsByMemberAsync(Guid memberId);

        /// <summary>
        /// Cancels an existing reservation.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if cancellation window has passed.</exception>
        Task<Result> CancelReservationAsync(Guid id);
    }
}