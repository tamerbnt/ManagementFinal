using System.Threading.Tasks;

namespace Management.Application.Interfaces.App
{
    /// <summary>
    /// Service for playing system sounds and audio notifications.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Plays a success sound.
        /// </summary>
        Task PlaySuccessAsync();

        /// <summary>
        /// Plays a failure/denied sound.
        /// </summary>
        Task PlayFailureAsync();
    }
}
