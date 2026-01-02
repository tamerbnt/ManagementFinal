using System;
using System.Threading.Tasks;

namespace Management.Domain.Services
{
    /// <summary>
    /// Service for monitoring session expiry and triggering refresh.
    /// </summary>
    public interface ISessionMonitorService
    {
        /// <summary>
        /// Starts monitoring the session for expiry.
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stops monitoring the session.
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Event raised when the session has expired and cannot be refreshed.
        /// </summary>
        event EventHandler<SessionExpiredEventArgs>? SessionExpired;

        /// <summary>
        /// Event raised when the session was successfully refreshed.
        /// </summary>
        event EventHandler? SessionRefreshed;
    }

    public class SessionExpiredEventArgs : EventArgs
    {
        public string Message { get; set; } = "Your session has expired. Please log in again.";
    }
}
