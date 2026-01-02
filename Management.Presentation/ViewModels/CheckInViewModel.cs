using System;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using System.Windows.Input;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Services;
using Management.Application.Services;
using Management.Presentation.Extensions;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class CheckInViewModel : ViewModelBase, IModalViewModel
    {
        private readonly IAccessEventService _accessEventService;
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;

        public ModalSize PreferredSize => ModalSize.Small;

        public Task<bool> CanCloseAsync() => Task.FromResult(true);

        private string _cardId = string.Empty;
        public string CardId
        {
            get => _cardId;
            set => SetProperty(ref _cardId, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set => SetProperty(ref _isSuccess, value);
        }

        public ICommand CheckInCommand { get; }
        public ICommand CancelCommand { get; }

        public CheckInViewModel(
            IAccessEventService accessEventService,
            IModalNavigationService modalService,
            INotificationService notificationService)
        {
            _accessEventService = accessEventService;
            _modalService = modalService;
            _notificationService = notificationService;

            CheckInCommand = new AsyncRelayCommand(ExecuteCheckInAsync, CanExecuteCheckIn);
            CancelCommand = new RelayCommand(async () => await _modalService.CloseCurrentModalAsync());
        }

        private bool CanExecuteCheckIn() => !string.IsNullOrWhiteSpace(CardId);

        private async Task ExecuteCheckInAsync()
        {
            if (string.IsNullOrWhiteSpace(CardId)) return;

            StatusMessage = "Processing...";
            IsSuccess = false;

            var result = await _accessEventService.ProcessAccessRequestAsync(CardId, Guid.Empty);

            if (result.IsSuccess && result.Value.IsAccessGranted)
            {
                IsSuccess = true;
                StatusMessage = $"Check-in Successful: {result.Value.MemberName}";
                _notificationService.ShowSuccess($"Checked in {result.Value.MemberName}");
                
                await Task.Delay(1500);
                await _modalService.CloseCurrentModalAsync();
            }
            else
            {
                IsSuccess = false;
                string reason = result.IsSuccess ? result.Value.FailureReason : result.Error.Message;
                StatusMessage = $"Access Denied: {reason}";
                _notificationService.ShowError("Check-in Failed");
            }
        }
    }
}
