using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Services
{
    public interface IReservationService
    {
        /// <summary>
        /// Retrieves bookings falling within the specified date range for the History/Schedule view.
        /// </summary>
        Task<List<ReservationDto>> GetReservationsByRangeAsync(DateTime start, DateTime end);

        /// <summary>
        /// Retrieves upcoming reservations for a specific member.
        /// </summary>
        Task<List<ReservationDto>> GetReservationsByMemberAsync(Guid memberId);

        /// <summary>
        /// Cancels an existing reservation.
        /// </summary>
        /// <exception cref="Management.Domain.Exceptions.BusinessRuleViolationException">Thrown if cancellation window has passed.</exception>
        Task CancelReservationAsync(Guid id);
    }
}