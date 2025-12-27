// Management.Presentation/ViewModels/ToastViewModel.cs
using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Management.Presentation.ViewModels
{
    // Ensure this Enum exists in your namespace, or move it here
    public enum ToastType { Success, Error, Warning, Info }

    public partial class ToastViewModel : ObservableObject
    {
        // 1. Existing Observable Properties
        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private string _styleKey = "ToastInfoStyle";

        [ObservableProperty]
        private bool _isMouseOver;

        [ObservableProperty]
        private bool _isExiting;

        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private DateTime _createdAt = DateTime.Now;

        // 2. Added Type property as requested
        [ObservableProperty]
        private ToastType _type;

        // 3. FIX: Changed from { get; } to { get; set; } 
        // This allows NotificationService to overwrite it with its own logic.
        public ICommand DismissCommand { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        // 4. FIX: Added Empty Constructor
        // This fixes the "No argument corresponds..." error in your Service
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

        partial void OnIsMouseOverChanged(bool value)
        {
            // Logic handled by binding in Service
        }
    }
}