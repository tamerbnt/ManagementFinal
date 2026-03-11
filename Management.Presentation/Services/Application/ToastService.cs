using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Management.Application.Interfaces.App;
using Management.Application.Stores;
using Management.Presentation.Models;

namespace Management.Presentation.Services.Application
{
    public class ToastService : IToastService
    {
        public ObservableCollection<ToastMessage> ActiveToasts { get; } = new();
        
        private readonly IDispatcher _dispatcher;
        private readonly NotificationStore _notificationStore;

        public ToastService(IDispatcher dispatcher, NotificationStore notificationStore)
        {
            _dispatcher = dispatcher;
            _notificationStore = notificationStore;
        }

        public void ShowSuccess(string message, string? title = null) => 
            AddToast(message, title ?? "Success", ToastType.Success);

        public void ShowError(string message, string? title = null) => 
            AddToast(message, title ?? "Error", ToastType.Error);

        public void ShowWarning(string message, string? title = null) => 
            AddToast(message, title ?? "Warning", ToastType.Warning);

        public void ShowInfo(string message, string? title = null) => 
            AddToast(message, title ?? "Info", ToastType.Info);

        private void AddToast(string message, string title, ToastType type)
        {
            var toast = new ToastMessage
            {
                Message = message,
                Title = title,
                Type = type
            };

            // Record in persistent history
            var storeType = type switch
            {
                ToastType.Success => Management.Application.Stores.NotificationType.Success,
                ToastType.Error => Management.Application.Stores.NotificationType.Error,
                ToastType.Warning => Management.Application.Stores.NotificationType.Warning,
                ToastType.Info => Management.Application.Stores.NotificationType.Info,
                _ => Management.Application.Stores.NotificationType.Info
            };
            _notificationStore.Add(message, title, storeType);

            // Non-blocking UI update
            _ = _dispatcher.InvokeAsync(() => ActiveToasts.Add(toast));

            // Auto-hide after 5 seconds
            _ = _dispatcher.InvokeAsync(() => 
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                timer.Tick += (s, e) =>
                {
                    _ = _dispatcher.InvokeAsync(() => ActiveToasts.Remove(toast));
                    timer.Stop();
                };
                timer.Start();
            });
        }
    }
}
