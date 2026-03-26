using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Management.Application.Stores;
using Management.Domain.Services;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Win32Dialogs = Microsoft.Win32;
using WpfApp = System.Windows.Application;


namespace Management.Presentation.Services
{
    public class DialogService : Management.Domain.Services.IDialogService
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly IModalNavigationService _modalNavigationService;
        private readonly IServiceProvider _serviceProvider;

        public DialogService(
            ModalNavigationStore modalNavigationStore,
            IModalNavigationService modalNavigationService,
            IServiceProvider serviceProvider)
        {
            _modalNavigationStore = modalNavigationStore;
            _modalNavigationService = modalNavigationService;
            _serviceProvider = serviceProvider;
        }

        // =================================================================
        // APP-STYLE OVERLAYS
        // =================================================================

        public async Task<bool> ShowConfirmationAsync(
            string title,
            string message,
            string confirmText = "Confirm",
            string cancelText = "Cancel",
            bool isDestructive = false)
        {
            // Thread safety for UI Store access
            if (!WpfApp.Current.Dispatcher.CheckAccess())
            {
                // Add a second 'await' to unwrap the Task<bool> returned by the inner method
                return await await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                    ShowConfirmationAsync(title, message, confirmText, cancelText, isDestructive));
            }

            // Resolve the shared modal VM
            var vm = _serviceProvider.GetRequiredService<Management.Presentation.ViewModels.Shared.ConfirmationModalViewModel>();
            vm.Configure(title, message, confirmText, cancelText, isDestructive);
            vm.IsAlert = false;

            // HYBRID FIX: Check if we are inside a Window-based modal (e.g. AppointmentDetailModal)
            // If so, we MUST use a window-based confirmation to avoid Z-index issues.
            var activeWindow = WpfApp.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
            bool isInsideWindowModal = activeWindow != null && activeWindow != WpfApp.Current.MainWindow;

            if (isInsideWindowModal)
            {
                var config = new Management.Presentation.ViewModels.Shared.ConfirmationModalConfig
                {
                    Title = title,
                    Message = message,
                    ConfirmText = confirmText,
                    CancelText = cancelText,
                    IsDestructive = isDestructive,
                    IsAlert = false
                };

                return await _modalNavigationService.OpenModalWithResultAsync<
                    Management.Presentation.ViewModels.Shared.ConfirmationModalViewModel, bool>(
                    ModalSize.Small, config);
            }

            // Use the original overlay-based async Open method on the store
            var modalResult = await _modalNavigationStore.OpenAsync(vm);
            return modalResult.IsSuccess;
        }

        public async Task ShowAlertAsync(string title, string message, string buttonText = "OK", bool isSuccess = false)
        {
            if (!WpfApp.Current.Dispatcher.CheckAccess())
            {
                // Await the task returned by the inner ShowAlertAsync call
                await await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                    ShowAlertAsync(title, message, buttonText, isSuccess));
                return;
            }

            // Resolve the shared modal VM
            var vm = _serviceProvider.GetRequiredService<Management.Presentation.ViewModels.Shared.ConfirmationModalViewModel>();
            vm.Configure(title, message, buttonText, string.Empty, false);
            vm.IsAlert = true;
            vm.IsSuccess = isSuccess;

            // HYBRID FIX: Check if we are inside a Window-based modal
            var activeWindow = WpfApp.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);
            bool isInsideWindowModal = activeWindow != null && activeWindow != WpfApp.Current.MainWindow;

            if (isInsideWindowModal)
            {
                var config = new Management.Presentation.ViewModels.Shared.ConfirmationModalConfig
                {
                    Title = title,
                    Message = message,
                    ConfirmText = buttonText,
                    CancelText = string.Empty,
                    IsDestructive = false,
                    IsAlert = true,
                    IsSuccess = isSuccess
                };

                await _modalNavigationService.OpenModalAsync<Management.Presentation.ViewModels.Shared.ConfirmationModalViewModel>(
                    ModalSize.Small, config);
                return;
            }

            // Use the async Open method on the store
            await _modalNavigationStore.OpenAsync(vm);
        }

        public async Task<object?> ShowCustomDialogAsync<TViewModel>(object? parameter = null)
            where TViewModel : class
        {
            if (!WpfApp.Current.Dispatcher.CheckAccess())
            {
                return await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                    ShowCustomDialogAsync<TViewModel>(parameter));
            }

            try
            {
                var modalResult = await _modalNavigationStore.OpenAsync<TViewModel>(parameter);
                return modalResult.Data;
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Error", $"Failed to open dialog: {ex.Message}");
                return null;
            }
        }

        // =================================================================
        // NATIVE OS DIALOGS
        // =================================================================

        public async Task<string?> ShowOpenFileDialogAsync(
            string filter = "All files (*.*)|*.*",
            string? initialDirectory = null,
            CancellationToken cancellationToken = default)
        {
            return await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Win32Dialogs.OpenFileDialog
                {
                    Filter = filter,
                    Multiselect = false,
                    InitialDirectory = initialDirectory ?? string.Empty
                };

                bool? result = dialog.ShowDialog(WpfApp.Current.MainWindow);
                return result == true ? dialog.FileName : null;
            });
        }

        public async Task<string?> ShowSaveFileDialogAsync(
            string defaultName,
            string filter = "All files (*.*)|*.*",
            string? initialDirectory = null,
            CancellationToken cancellationToken = default)
        {
            return await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Win32Dialogs.SaveFileDialog
                {
                    FileName = defaultName,
                    Filter = filter,
                    InitialDirectory = initialDirectory ?? string.Empty,
                    OverwritePrompt = true
                };

                bool? result = dialog.ShowDialog(WpfApp.Current.MainWindow);
                return result == true ? dialog.FileName : null;
            });
        }

        public async Task<string?> ShowFolderDialogAsync(
            string description = "Select a folder",
            string? initialDirectory = null,
            CancellationToken cancellationToken = default)
        {
            return await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Win32Dialogs.OpenFolderDialog
                {
                    Title = description,
                    InitialDirectory = initialDirectory ?? string.Empty,
                    Multiselect = false
                };

                bool? result = dialog.ShowDialog(WpfApp.Current.MainWindow);
                return result == true ? dialog.FolderName : null;
            });
        }
    }
}
