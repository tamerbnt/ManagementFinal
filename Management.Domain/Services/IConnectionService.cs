using System;
using System.Threading.Tasks;

namespace Management.Domain.Services
{
    public interface IConnectionService
    {
        /// <summary>
        /// Fired when internet connectivity status changes (True = Online, False = Offline).
        /// </summary>
        event Action<bool> ConnectionStatusChanged;

        /// <summary>
        /// Fired when Supabase cloud connectivity status changes.
        /// </summary>
        event Action<bool> SupabaseStatusChanged;

        /// <summary>
        /// Manually checks if the internet is reachable.
        /// </summary>
        Task<bool> IsInternetAvailableAsync();
        
        /// <summary>
        /// Checks if Supabase cloud service is reachable.
        /// </summary>
        Task<bool> CanReachSupabaseAsync();
        
        /// <summary>
        /// Returns current online status (synchronous).
        /// </summary>
        bool IsOnline();
    }
}
