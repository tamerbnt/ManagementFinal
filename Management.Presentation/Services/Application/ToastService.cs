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

        public void ShowSuccess(string message, string undoLabel, Func<Task> undoAction)
        {
            var toast = new ToastMessage
            {
                Message = message,
                Title = "Success",
                Type = ToastType.Success,
                HasUndo = true,
                UndoLabel = undoLabel
            };

            toast.UndoCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                try 
                {
                    await undoAction();
                    await _dispatcher.InvokeAsync(() => ActiveToasts.Remove(toast));
                }
                catch (Exception ex)
                {
                    ShowError($"Undo failed: {ex.Message}");
                }
            });

            AddToastCore(toast);
        }

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

            AddToastCore(toast);
        }

        private void AddToastCore(ToastMessage toast)
        {
            // Record in persistent history
            var storeType = toast.Type switch
            {
                ToastType.Success => Management.Application.Stores.NotificationType.Success,
                ToastType.Error => Management.Application.Stores.NotificationType.Error,
                ToastType.Warning => Management.Application.Stores.NotificationType.Warning,
                ToastType.Info => Management.Application.Stores.NotificationType.Info,
                _ => Management.Application.Stores.NotificationType.Info
            };
            _notificationStore.Add(toast.Message, toast.Title, storeType);

            // Initialize DismissCommand
            toast.DismissCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                _ = TriggerExitAsync(toast);
            });

            // Non-blocking UI update
            _ = _dispatcher.InvokeAsync(() => ActiveToasts.Add(toast));

            // Auto-hide
            var duration = toast.HasUndo ? 7 : 5; // 7 seconds for undo as per requirement
            _ = Task.Run(async () => 
            {
                await Task.Delay(TimeSpan.FromSeconds(duration));
                await TriggerExitAsync(toast);
            });
        }

        private async Task TriggerExitAsync(ToastMessage toast)
        {
            if (toast.IsExiting) return;

            await _dispatcher.InvokeAsync(() => toast.IsExiting = true);
            
            // Wait for XAML animation to complete (0.3s in MainWindow.xaml)
            await Task.Delay(350); 
            
            await _dispatcher.InvokeAsync(() => ActiveToasts.Remove(toast));
        }
    }
}
