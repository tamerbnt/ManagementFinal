// Management.Presentation/ViewModels/ToastViewModel.cs
using System;
using System.Windows.Input;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels
{
    // Ensure this Enum exists in your namespace, or move it here
    public enum ToastType { Success, Error, Warning, Info }

    public class ToastViewModel : ViewModelBase
    {
        // 1. Observable Properties with manual backing fields
        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private string _styleKey = "ToastInfoStyle";
        public string StyleKey
        {
            get => _styleKey;
            set => SetProperty(ref _styleKey, value);
        }

        private bool _isMouseOver;
        public bool IsMouseOver
        {
            get => _isMouseOver;
            set
            {
                if (SetProperty(ref _isMouseOver, value))
                {
                    OnIsMouseOverChanged(value);
                }
            }
        }

        private bool _isExiting;
        public bool IsExiting
        {
            get => _isExiting;
            set => SetProperty(ref _isExiting, value);
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        private DateTime _createdAt = DateTime.Now;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        // 2. Type property
        private ToastType _type;
        public ToastType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        // 3. Command property - allows NotificationService to overwrite it
        public ICommand DismissCommand { get; set; } = null!;

        public string Number { get; set; } = string.Empty;
        public Guid Id { get; set; } = Guid.NewGuid();

        // 4. Empty Constructor
        public ToastViewModel()
        {
            // Default command if none is injected
            DismissCommand = new RelayCommand(DismissInternal);
        }

        // Keep this constructor for manual usage elsewhere
        public ToastViewModel(string message, string styleKey = "ToastInfoStyle") : this()
        {
            Message = message;
            StyleKey = styleKey;
        }

        // Internal helper if you use the internal dismissal logic
        internal Action<ToastViewModel>? OnDismissRequested { get; set; }

        private void DismissInternal()
        {
            OnDismissRequested?.Invoke(this);
        }

        private void OnIsMouseOverChanged(bool value)
        {
            // Logic handled by binding in Service
        }
    }
}