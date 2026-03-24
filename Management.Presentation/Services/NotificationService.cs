using System;
using System.Diagnostics;

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
using Microsoft.Extensions.Logging;

namespace Management.Presentation.Services
{
    public class NotificationService : INotificationService, Management.Application.Interfaces.App.IToastService, IDisposable
    {
        private readonly NotificationStore _notificationStore; // Retained for future badge sync
        private readonly ILogger<NotificationService> _logger;
        private readonly DispatcherTimer _autoDismissTimer;

        // Toast Configuration (Design System Section 34)
        private readonly ObservableCollection<ToastViewModel> _activeToasts = new();
        public ObservableCollection<ToastViewModel> ActiveToasts => _activeToasts;
        private const int MaxVisibleToasts = 1;
        private const int ToastAutoDismissSeconds = 3;
        private const int AnimationDurationMs = 300; // Matches DurationMedium

        private bool _soundEnabled = true;



        public NotificationService(NotificationStore notificationStore, ILogger<NotificationService> logger)
        {
            _notificationStore = notificationStore;
            _logger = logger;

            // Timer checks every 500ms to clean up expired toasts
            _autoDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _autoDismissTimer.Tick += OnAutoDismissTick;
            _autoDismissTimer.Start();
        }


        // --- Interface Implementation ---

        private void Show(ToastType type, string message, string? title = null) 
            => ShowToast(type, string.IsNullOrEmpty(title) ? message : $"{title}: {message}");

        // Satisfy IToastService (message first, optional title)
        public void ShowSuccess(string message, string? title = null)
        {
            Debug.WriteLine($"[TOAST] ShowSuccess called. Message='{message}', HasUndoAction=False");
            Show(ToastType.Success, message, title);
        }

        public void ShowError(string message, string? title = null) => Show(ToastType.Error, message, title);
        public void ShowWarning(string message, string? title = null) => Show(ToastType.Warning, message, title);
        public void ShowInfo(string message, string? title = null) => Show(ToastType.Info, message, title);

        // Explicitly satisfy INotificationService to resolve signature/param-name ambiguity
        void INotificationService.ShowSuccess(string message) => Show(ToastType.Success, message);
        void INotificationService.ShowSuccess(string message, Func<Task> undoAction, string undoLabel) => ShowSuccess(message, undoAction, undoLabel);
        void INotificationService.ShowError(string message) => Show(ToastType.Error, message);
        void INotificationService.ShowError(string title, string message) => Show(ToastType.Error, message, title);
        void INotificationService.ShowWarning(string message) => Show(ToastType.Warning, message);
        void INotificationService.ShowInfo(string message) => Show(ToastType.Info, message);

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
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
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
                    var oldest = _activeToasts.FirstOrDefault();
                    if (oldest != null)
                    {
                        _activeToasts.Remove(oldest);
                    }
                }

                _activeToasts.Add(toast);

                if (_soundEnabled) PlayNotificationSound(type);
            });
        }

        public void ShowSuccess(string message, Func<Task> undoAction, string undoLabel = "Undo")
        {
            ArgumentNullException.ThrowIfNull(undoAction);
            if (string.IsNullOrWhiteSpace(message)) return;

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var toast = new ToastViewModel
                {
                    Id = Guid.NewGuid(),
                    Type = ToastType.Success,
                    Message = message,
                    CreatedAt = DateTime.Now,
                    IsPaused = false,
                    IsExiting = false,
                    UndoLabel = undoLabel
                };
                
                toast.DismissCommand = new AsyncRelayCommand(() => DismissToastAsync(toast.Id));

                toast.UndoCommand = new AsyncRelayCommand(async () =>
                {
                    // Start Exit immediately to show progress
                    toast.IsExiting = true; 
                    try { await undoAction(); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Undo] Undo action failed for message: {Message}", message);
                    }
                    // Wait for animation to finish after logic if it wasn't already dismissed
                    await DismissToastAsync(toast.Id);
                });

                if (_activeToasts.Count >= MaxVisibleToasts)
                {
                    var oldest = _activeToasts.FirstOrDefault(t => !t.IsExiting);
                    if (oldest != null) _activeToasts.Remove(oldest);
                }

                _activeToasts.Add(toast);
                if (_soundEnabled) PlayNotificationSound(ToastType.Success);
            });
        }

        private async Task DismissToastAsync(Guid id)
        {
            // Always run on UI thread
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => DismissToastAsync(id));
                return;
            }

            var toast = _activeToasts.FirstOrDefault(t => t.Id == id);
            if (toast == null) return;

            // Step 1: Trigger Animation once
            if (!toast.IsExiting)
            {
                toast.IsExiting = true;
                // Step 2: WAIT for exactly the time the view needs
                await Task.Delay(AnimationDurationMs);
            }

            // Step 3: Delete from collection (idempotent removal)
            _activeToasts.Remove(toast);
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
                            (now - t.CreatedAt).TotalSeconds >= (t.HasUndo ? 6 : ToastAutoDismissSeconds))
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
        }
    }
}
