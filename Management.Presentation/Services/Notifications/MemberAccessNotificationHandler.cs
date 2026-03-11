using System.Threading;
using System.Threading.Tasks;
using Management.Application.Interfaces.App;
using Management.Application.Notifications;
using Management.Domain.Services;
using Management.Presentation.ViewModels.Members;
using MediatR;

namespace Management.Presentation.Services.Notifications
{
    public class MemberAccessNotificationHandler : INotificationHandler<MemberScannedNotification>
    {
        private readonly IDialogService _dialogService;
        private readonly IAudioService _audioService;

        public MemberAccessNotificationHandler(IDialogService dialogService, IAudioService audioService)
        {
            _dialogService = dialogService;
            _audioService = audioService;
        }

        public async Task Handle(MemberScannedNotification notification, CancellationToken cancellationToken)
        {
            // 1. Play appropriate sound
            if (notification.IsAccessGranted)
            {
                await _audioService.PlaySuccessAsync();
            }
            else
            {
                await _audioService.PlayFailureAsync();
            }

            // 2. Show the dedicated feedback popup
            await _dialogService.ShowCustomDialogAsync<MemberAccessViewModel>(notification);
        }
    }
}
