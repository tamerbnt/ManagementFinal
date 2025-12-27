// Management.Presentation/Services/INotificationService.cs
using System.Collections.ObjectModel;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Services
{
    public interface INotificationService
    {
        ObservableCollection<ToastViewModel> ActiveToasts { get; }

        void ShowSuccess(string message);
        void ShowError(string message);
        void ShowWarning(string message);
        void ShowInfo(string message);
    }
}