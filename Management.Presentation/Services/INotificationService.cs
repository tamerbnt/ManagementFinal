// Management.Presentation/Services/INotificationService.cs
using System.Collections.ObjectModel;
using Management.Presentation.ViewModels;
using Management.Presentation.ViewModels.Shared;

namespace Management.Presentation.Services
{
    public enum NotificationType { Success, Error, Warning, Info }

    public interface INotificationService
    {
        ObservableCollection<ToastViewModel> ActiveToasts { get; }

        void ShowSuccess(string message);
        void ShowSuccess(string message, string undoLabel, System.Func<System.Threading.Tasks.Task> undoAction);
        void ShowError(string message);
        void ShowError(string title, string message); // Overload
        void ShowWarning(string message);
        void ShowInfo(string message);
        void ShowNotification(string message, NotificationType type);

        // Phase 2 Overlay
        string? CurrentMessage { get; }
        bool HasUndo { get; }
        System.Windows.Input.ICommand UndoCommand { get; }
        System.Windows.Input.ICommand DismissCommand { get; }
        void ShowUndoNotification(string message, System.Func<System.Threading.Tasks.Task> undoAction, System.Func<System.Threading.Tasks.Task> finalAction);
    }

}
