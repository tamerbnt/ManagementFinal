using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Management.Presentation.Controls;

namespace Management.Presentation.Services
{
    public interface IToastNotificationService
    {
        void ShowSuccess(string title, string message, int durationMs = 4000);
        void ShowError(string title, string message, int durationMs = 4000);
        void ShowWarning(string title, string message, int durationMs = 4000);
    }

    public class ToastNotificationService : IToastNotificationService
    {
        private Panel? _toastContainer;
        private readonly ObservableCollection<ToastNotification> _activeToasts = new();
        private const int MaxVisibleToasts = 3;

        public void Initialize(Panel toastContainer)
        {
            _toastContainer = toastContainer;
        }

        public void ShowSuccess(string title, string message, int durationMs = 4000)
        {
            ShowToast(ToastNotification.ToastType.Success, title, message, durationMs);
        }

        public void ShowError(string title, string message, int durationMs = 4000)
        {
            ShowToast(ToastNotification.ToastType.Error, title, message, durationMs);
        }

        public void ShowWarning(string title, string message, int durationMs = 4000)
        {
            ShowToast(ToastNotification.ToastType.Warning, title, message, durationMs);
        }

        private void ShowToast(ToastNotification.ToastType type, string title, string message, int durationMs)
        {
            if (_toastContainer == null)
            {
                // Fallback: try to find toast container in current window
                var window = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (window?.Content is Grid rootGrid)
                {
                    _toastContainer = rootGrid.Children.OfType<Panel>().FirstOrDefault(p => p.Name == "ToastContainer");
                }

                if (_toastContainer == null)
                {
                    System.Diagnostics.Debug.WriteLine("ToastNotificationService: Toast container not found");
                    return;
                }
            }

            // Create toast
            var toast = new ToastNotification();
            toast.Show(type, title, message);

            // Add to container
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _toastContainer.Children.Add(toast);
                _activeToasts.Add(toast);

                // Remove oldest if exceeding max
                if (_activeToasts.Count > MaxVisibleToasts)
                {
                    var oldest = _activeToasts.First();
                    oldest.Dismiss();
                    _activeToasts.Remove(oldest);
                }

                // Auto-dismiss after duration
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(durationMs)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    toast.Dismiss();
                    _activeToasts.Remove(toast);
                };
                timer.Start();
            });
        }
    }
}
