using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Stores;
using Management.Presentation.Stores;
using Management.Presentation.Services;
using System;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Shared
{
    public partial class NotificationDetailViewModel : ObservableObject, IInitializable<NotificationItem>
    {
        private readonly ModalNavigationStore _modalStore;

        [ObservableProperty]
        private NotificationItem? _notification;

        public IAsyncRelayCommand CloseCommand { get; }

        public NotificationDetailViewModel(ModalNavigationStore modalStore)
        {
            _modalStore = modalStore;
            CloseCommand = new AsyncRelayCommand(async () => await _modalStore.CloseAsync(ModalResult.Success()));
        }

        public Task InitializeAsync(NotificationItem parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            Notification = parameter;
            return Task.CompletedTask;
        }

        public void Configure(NotificationItem notification)
        {
            Notification = notification;
        }
    }
}
