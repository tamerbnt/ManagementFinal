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
        private readonly IServiceProvider _serviceProvider;

        public DialogService(
            ModalNavigationStore modalNavigationStore,
            IServiceProvider serviceProvider)
        {
            _modalNavigationStore = modalNavigationStore;
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

            // Create VM. The callback will trigger CloseAsync on the store.
            var vm = new ConfirmationViewModel(
                title,
                message,
                confirmText,
                cancelText,
                isDestructive,
                isAlert: false,
                resultCallback: async (result) =>
                {
                    // FIX: Call the async Close method on the store
                    await _modalNavigationStore.CloseAsync(result ? ModalResult.Success() : ModalResult.Cancel());
                });

            // FIX: Use the async Open method on the store
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

            var vm = new ConfirmationViewModel(
                title,
                message,
                buttonText,
                string.Empty, // No cancel button for alerts
                false,
                isAlert: true,
                resultCallback: async (result) => // Callback is still needed to close the modal
                {
                    await _modalNavigationStore.CloseAsync(ModalResult.Success());
                },
                isSuccess: isSuccess);

            // FIX: Use the async Open method on the store
            await _modalNavigationStore.OpenAsync(vm);
            // We don't await the result here because ShowAlertAsync is Task (void), not Task<bool>
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
