using System;
using System.Windows.Input;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels;

namespace Management.Presentation.ViewModels
{
    public class ConfirmationViewModel : ViewModelBase
    {
        private readonly Action<bool> _resultCallback;

        public string Title { get; }
        public string Message { get; }
        public string ConfirmText { get; }
        public string CancelText { get; }

        // Triggers Red/Destructive styling on Confirm button
        public bool IsDestructive { get; }

        // Hides the Cancel button for Info/Alerts
        public bool IsAlert { get; }

        public bool IsSuccess { get; }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public ConfirmationViewModel(
            string title,
            string message,
            string confirmText,
            string cancelText,
            bool isDestructive,
            bool isAlert,
            Action<bool> resultCallback,
            bool isSuccess = false)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            IsDestructive = isDestructive;
            IsAlert = isAlert;
            IsSuccess = isSuccess;
            _resultCallback = resultCallback;

            ConfirmCommand = new RelayCommand(() => _resultCallback(true));
            CancelCommand = new RelayCommand(() => _resultCallback(false));
        }
    }
}