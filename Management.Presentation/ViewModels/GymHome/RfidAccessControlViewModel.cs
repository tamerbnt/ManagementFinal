using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.Models;
using Management.Domain.Enums;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Management.Domain.Services;
using Management.Presentation.ViewModels.Members;
using Microsoft.Extensions.Logging;
using System.Linq;
using Management.Application.Services;
using Management.Application.Interfaces.App;

namespace Management.Presentation.ViewModels.GymHome
{
    public partial class RfidAccessControlViewModel : ViewModelBase, IModalAware
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly Management.Domain.Services.IDialogService _dialogService;
        private DispatcherTimer? _autoCloseTimer;

        [ObservableProperty]
        private ScanResult? _result;

        [ObservableProperty]
        private int _remainingTime = 5;

        [ObservableProperty]
        private bool _isAutoClosing;

        public bool AccessGranted => Result?.Status == AccessResult.Granted;
        public bool AccessDenied => Result?.Status == AccessResult.Denied;
        public bool AccessWarning => Result?.Status == AccessResult.Warning;
        public bool MemberExists => Result?.Member != null;
        public bool ShowRenewButton => (AccessDenied || AccessWarning) && MemberExists;
        public bool ShowCreateButton => AccessDenied && !MemberExists;

        public string AvatarInitials
        {
            get
            {
                if (Result?.Member == null) return "??";
                var nameParts = Result.Member.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (nameParts.Length == 0) return "??";
                if (nameParts.Length == 1) return nameParts[0][0].ToString().ToUpper();
                return (nameParts[0][0].ToString() + nameParts[^1][0].ToString()).ToUpper();
            }
        }

        public RfidAccessControlViewModel(
            ILogger<RfidAccessControlViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ModalNavigationStore modalNavigationStore,
            Management.Domain.Services.IDialogService dialogService) : base(logger, diagnosticService, toastService)
        {
            _modalNavigationStore = modalNavigationStore;
            _dialogService = dialogService;
            Title = "Access Control";
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            if (parameter is ScanResult scanResult)
            {
                Result = scanResult;
                
                // Trigger UI updates
                OnPropertyChanged(nameof(AccessGranted));
                OnPropertyChanged(nameof(AccessDenied));
                OnPropertyChanged(nameof(AccessWarning));
                OnPropertyChanged(nameof(MemberExists));
                OnPropertyChanged(nameof(ShowRenewButton));
                OnPropertyChanged(nameof(ShowCreateButton));
                OnPropertyChanged(nameof(AvatarInitials));

                if (AccessGranted)
                {
                    StartAutoCloseTimer();
                }
            }
            await Task.CompletedTask;
        }

        private void StartAutoCloseTimer()
        {
            IsAutoClosing = true;
            RemainingTime = 5;
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoCloseTimer.Tick += (s, e) =>
            {
                RemainingTime--;
                if (RemainingTime <= 0)
                {
                    _autoCloseTimer.Stop();
                    _ = CloseAsync();
                }
            };
            _autoCloseTimer.Start();
        }

        [RelayCommand]
        private async Task CloseAsync()
        {
            _autoCloseTimer?.Stop();
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }

        [RelayCommand]
        private async Task RenewMembershipAsync()
        {
            _autoCloseTimer?.Stop();
            if (Result?.Member != null)
            {
                var memberId = Result.Member.Id;
                await _modalNavigationStore.CloseAsync(ModalResult.Success());
                // Open the QuickRegistration Form in Renew/Modify mode instead of the detailed view
                await _dialogService.ShowCustomDialogAsync<QuickRegistrationViewModel>(memberId);
            }
        }

        [RelayCommand]
        private async Task CreateMemberAsync()
        {
            _autoCloseTimer?.Stop();
            await _modalNavigationStore.CloseAsync(ModalResult.Success());
            await _dialogService.ShowCustomDialogAsync<QuickRegistrationViewModel>();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoCloseTimer?.Stop();
            }
            base.Dispose(disposing);
        }
    }
}
