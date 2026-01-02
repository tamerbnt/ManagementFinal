using System.Threading;
using System.Threading.Tasks;

namespace Management.Domain.Services
{
    public interface IDialogService
    {
        /// <summary>
        /// Shows a modal confirmation overlay (Yes/No style).
        /// </summary>
        Task<bool> ShowConfirmationAsync(
            string title,
            string message,
            string confirmText = "Confirm",
            string cancelText = "Cancel",
            bool isDestructive = false);

        /// <summary>
        /// Shows a single-button alert (OK only).
        /// </summary>
        Task ShowAlertAsync(
            string title,
            string message,
            string buttonText = "OK",
            bool isSuccess = false);

        /// <summary>
        /// Shows a custom content modal (e.g., Product Form, Checkout).
        /// </summary>
        Task ShowCustomDialogAsync<TViewModel>(object? parameter = null)
            where TViewModel : class;

        /// <summary>
        /// Opens a Native OS File Picker to select a file.
        /// </summary>
        /// <returns>The full file path, or null if cancelled.</returns>
        Task<string?> ShowOpenFileDialogAsync(
            string filter = "All files (*.*)|*.*",
            string? initialDirectory = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a Native OS Save Dialog.
        /// </summary>
        /// <returns>The full file path, or null if cancelled.</returns>
        Task<string?> ShowSaveFileDialogAsync(
            string defaultName,
            string filter = "All files (*.*)|*.*",
            string? initialDirectory = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a Native OS Folder Picker.
        /// </summary>
        Task<string?> ShowFolderDialogAsync(
            string description = "Select a folder",
            string? initialDirectory = null,
            CancellationToken cancellationToken = default);
    }
}