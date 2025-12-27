using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;

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
        }

        // --- Interface Implementation ---

        public void ShowSuccess(string message) => ShowToast(ToastType.Success, message);
        public void ShowError(string message) => ShowToast(ToastType.Error, message);
        public void ShowInfo(string message) => ShowToast(ToastType.Info, message);
        public void ShowWarning(string message) => ShowToast(ToastType.Warning, message);

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

                // Wire up the Dismiss Command
                toast.DismissCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => DismissToast(toast.Id));

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

        private async void DismissToast(Guid id)
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

        private void OnAutoDismissTick(object sender, EventArgs e)
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
                DismissToast(toast.Id);
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
        }
    }

    // --- VIEW MODEL ---



    public class ToastNotificationViewModel : ViewModelBase
    {
        public Guid Id { get; set; }
        public ToastType Type { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICommand DismissCommand { get; set; }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        private bool _isExiting;
        public bool IsExiting
        {
            get => _isExiting;
            set => SetProperty(ref _isExiting, value);
        }
    }
}