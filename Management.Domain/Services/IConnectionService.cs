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
        /// Manually checks if the internet is reachable.
        /// </summary>
        Task<bool> IsInternetAvailableAsync();
    }
}