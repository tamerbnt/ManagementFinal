using System.Media;
using System.Threading.Tasks;
using Management.Application.Interfaces.App;
using System.Media;

namespace Management.Infrastructure.Services.Audio
{
    public class AudioService : IAudioService
    {
        public Task PlaySuccessAsync()
        {
            return Task.Run(() => 
            {
                SystemSounds.Asterisk.Play();
            });
        }

        public Task PlayFailureAsync()
        {
            return Task.Run(() => 
            {
                SystemSounds.Hand.Play();
            });
        }
    }
}
