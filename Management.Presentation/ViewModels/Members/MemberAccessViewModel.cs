using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Notifications;
using Management.Domain.Enums;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Members
{
    public partial class MemberAccessViewModel : ViewModelBase
    {
        private readonly ModalNavigationStore _modalNavigationStore;

        [ObservableProperty]
        private MemberDto? _member;

        [ObservableProperty]
        private string _cardId = string.Empty;

        [ObservableProperty]
        private bool _isAccessGranted;

        [ObservableProperty]
        private AccessStatus _accessStatus;

        [ObservableProperty]
        private string? _failureReason;

        [ObservableProperty]
        private int _remainingDays;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public MemberAccessViewModel(ModalNavigationStore modalNavigationStore)
        {
            _modalNavigationStore = modalNavigationStore;
            Title = "Access Control";
        }

        public override Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is MemberScannedNotification notification)
            {
                Member = notification.Member;
                CardId = notification.CardId;
                IsAccessGranted = notification.IsAccessGranted;
                AccessStatus = notification.Status;
                FailureReason = notification.FailureReason;

                if (Member != null)
                {
                    RemainingDays = (Member.ExpirationDate - DateTime.UtcNow).Days;
                }

                StatusMessage = AccessStatus switch
                {
                    AccessStatus.Granted => "Access Granted",
                    AccessStatus.Warning => "Access Granted (Expiring Soon)",
                    AccessStatus.Denied => "Access Denied",
                    _ => IsAccessGranted ? "Access Granted" : "Access Denied"
                };

                // Auto-close after 4 seconds
                _ = Task.Delay(4000, cancellationToken).ContinueWith(_ => CloseCommand.Execute(null), cancellationToken);
            }

            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task CloseAsync()
        {
            await _modalNavigationStore.CloseAsync();
        }
    }
}
