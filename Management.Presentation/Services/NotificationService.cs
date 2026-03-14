using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;
using Management.Presentation.ViewModels.Shared;
using Management.Presentation.Models;

namespace Management.Presentation.Services
{
    public class NotificationService : INotificationService, IDisposable
    {
        private readonly NotificationStore _notificationStore; // Retained for future badge sync
        private readonly DispatcherTimer _autoDismissTimer;

        // Toast Configuration (Design System Section 34)
        private readonly ObservableCollection<ToastViewModel> _activeToasts = new();
        public ObservableCollection<ToastViewModel> ActiveToasts => _activeToasts;
        private const int MaxVisibleToasts = 3;
        private const int ToastAutoDismissSeconds = 5;
        private const int AnimationDurationMs = 300; // Matches DurationMedium

        private bool _soundEnabled = true;



        public NotificationService(NotificationStore notificationStore)
        {
            _notificationStore = notificationStore;

            // Timer checks every 500ms to clean up expired toasts
            _autoDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _autoDismissTimer.Tick += OnAutoDismissTick;
            _autoDismissTimer.Start();

            UndoCommand = new AsyncRelayCommand(ExecuteUndo);
            DismissCommand = new RelayCommand(ExecuteDismiss);
        }

        // --- Phase 2 Overlay Implementation ---

        private string? _currentMessage;
        public string? CurrentMessage
        {
            get => _currentMessage;
            private set { _currentMessage = value; OnPropertyChanged(); }
        }

        private bool _hasUndo;
        public bool HasUndo
        {
            get => _hasUndo;
            private set { _hasUndo = value; OnPropertyChanged(); }
        }

        public ICommand UndoCommand { get; }
        public ICommand DismissCommand { get; }

        private Func<Task>? _pendingUndoAction;
        private Func<Task>? _pendingFinalAction;
        private DispatcherTimer? _undoTimer;

        public void ShowUndoNotification(string message, Func<Task> undoAction, Func<Task> finalAction)
        {
            CurrentMessage = message;
            HasUndo = undoAction != null;
            _pendingUndoAction = undoAction;
            _pendingFinalAction = finalAction;

            if (_undoTimer != null) _undoTimer.Stop();
            _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _undoTimer.Tick += async (s, e) =>
            {
                _undoTimer.Stop();
                if (_pendingFinalAction != null) await _pendingFinalAction();
                ExecuteDismiss();
            };
            _undoTimer.Start();
            
            // Trigger View Animation (Slide Down)
            // Implementation detail: MainWindow.xaml notification overlay will bind to this.
        }

        private async Task ExecuteUndo()
        {
            _undoTimer?.Stop();
            if (_pendingUndoAction != null) await _pendingUndoAction();
            ExecuteDismiss();
        }

        private void ExecuteDismiss()
        {
            _undoTimer?.Stop();
            CurrentMessage = null;
            HasUndo = false;
            _pendingUndoAction = null;
            _pendingFinalAction = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // --- Interface Implementation ---


        public void ShowSuccess(string message) => ShowToast(ToastType.Success, message);
        public void ShowError(string message) => ShowToast(ToastType.Error, message);
        public void ShowError(string title, string message) => ShowToast(ToastType.Error, $"{title}: {message}");
        public void ShowInfo(string message) => ShowToast(ToastType.Info, message);
        public void ShowWarning(string message) => ShowToast(ToastType.Warning, message);

        public void ShowNotification(string message, NotificationType type)
        {
            var toastType = type switch
            {
                NotificationType.Success => ToastType.Success,
                NotificationType.Error => ToastType.Error,
                NotificationType.Warning => ToastType.Warning,
                NotificationType.Info => ToastType.Info,
                _ => ToastType.Info
            };
            ShowToast(toastType, message);
        }

        // --- Core Logic ---

        private void ShowToast(ToastType type, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            // Safe Dispatch to UI Thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var toast = new ToastViewModel
                {
                    Id = Guid.NewGuid(),
                    Type = type,
                    Message = message,
                    CreatedAt = DateTime.Now,
                    IsPaused = false,
                    IsExiting = false
                };
                toast.DismissCommand = new AsyncRelayCommand(() => DismissToastAsync(toast.Id));

                // Queue Management: If full, force-dismiss the oldest one
                if (_activeToasts.Count >= MaxVisibleToasts)
                {
                    var oldest = _activeToasts.FirstOrDefault(t => !t.IsExiting);
                    if (oldest != null)
                    {
                        // Instant remove for overflow to keep UI snappy
                        _activeToasts.Remove(oldest);
                    }
                }

                _activeToasts.Add(toast);

                if (_soundEnabled) PlayNotificationSound(type);
            });
        }

        private async Task DismissToastAsync(Guid id)
        {
            if (System.Windows.Application.Current == null) return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var toast = _activeToasts.FirstOrDefault(t => t.Id == id);
                if (toast == null || toast.IsExiting) return;

                // 1. Trigger Exit Animation (View binds to IsExiting)
                toast.IsExiting = true;

                // 2. Wait for Animation to finish (300ms)
                await Task.Delay(AnimationDurationMs);

                // 3. Remove from Data
                _activeToasts.Remove(toast);
            });
        }

        public void PauseAutoDismiss(Guid id)
        {
            var toast = _activeToasts.FirstOrDefault(t => t.Id == id);
            if (toast != null) toast.IsPaused = true;
        }

        public void ResumeAutoDismiss(Guid id)
        {
            var toast = _activeToasts.FirstOrDefault(t => t.Id == id);
            if (toast != null)
            {
                toast.IsPaused = false;
                // Reset timer clock to give user time to read after un-pausing
                toast.CreatedAt = DateTime.Now.AddSeconds(-2);
            }
        }

        private void OnAutoDismissTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            // Find items that expired AND are not paused AND are not already exiting
            var expiredItems = _activeToasts
                .Where(t => !t.IsPaused &&
                            !t.IsExiting &&
                            (now - t.CreatedAt).TotalSeconds >= ToastAutoDismissSeconds)
                .ToList();

            foreach (var toast in expiredItems)
            {
                _ = DismissToastAsync(toast.Id);
            }
        }

        private void PlayNotificationSound(ToastType type)
        {
            try
            {
                // System Beeps for V1 Prototype
                // V2: Replace with MediaPlayer playing .wav files from Resources
                switch (type)
                {
                    case ToastType.Success: Console.Beep(523, 200); break; // High C
                    case ToastType.Error: Console.Beep(220, 300); break;   // Low A
                    case ToastType.Warning: Console.Beep(330, 300); break;
                    case ToastType.Info: Console.Beep(440, 200); break;
                }
            }
            catch { /* Audio failure should not crash app */ }
        }

        public void Dispose()
        {
            _autoDismissTimer?.Stop();
            _undoTimer?.Stop();
        }
    }
}
