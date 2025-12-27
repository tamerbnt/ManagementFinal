// ******************************************************************************************
//  Management.Presentation/Services/IModalNavigationService.cs
//  FINAL PRODUCTION VERSION – v1.2.0-production
//  Design System: Apple 2025 Edition – v1.2 FINAL (LOCKED)
//  Status: PRODUCTION READY
// ******************************************************************************************

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Presentation.Services
{
    #region Enums

    /// <summary>
    /// Modal size variants as per Design System §15.4
    /// </summary>
    public enum ModalSize
    {
        /// <summary>
        /// 640px width - Simple forms, confirmations
        /// </summary>
        Small = 640,

        /// <summary>
        /// 880px width - Member detail, staff detail
        /// </summary>
        Medium = 880,

        /// <summary>
        /// 1120px width - Complex forms, reports
        /// </summary>
        Large = 1120
    }

    /// <summary>
    /// Modal animation types as per Design System §5.2, §33.5
    /// </summary>
    public enum ModalAnimation
    {
        /// <summary>
        /// Default scale+fade animation (400ms)
        /// </summary>
        Default,

        /// <summary>
        /// Fade only (300ms) - For stacked modals
        /// </summary>
        FadeOnly,

        /// <summary>
        /// No animation - For reduced motion
        /// </summary>
        None
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Modal navigation events for external subscribers
    /// </summary>
    public class ModalNavigationEventArgs : EventArgs
    {
        public Type ViewModelType { get; }
        public ModalSize Size { get; }
        public int StackDepth { get; }
        public bool IsSuccessful { get; }
        public Exception? Error { get; }

        public ModalNavigationEventArgs(Type viewModelType, ModalSize size, int stackDepth,
                                       bool isSuccessful, Exception? error = null)
        {
            ViewModelType = viewModelType;
            Size = size;
            StackDepth = stackDepth;
            IsSuccessful = isSuccessful;
            Error = error;
        }
    }

    #endregion

    #region Interfaces

    /// <summary>
    /// Marker interface for modal ViewModels
    /// </summary>
    public interface IModalViewModel
    {
        /// <summary>
        /// Gets the modal size (can be overridden by service)
        /// </summary>
        ModalSize PreferredSize { get; }

        /// <summary>
        /// Called when the modal is about to close
        /// </summary>
        Task<bool> CanCloseAsync();
    }

    /// <summary>
    /// Interface for ViewModels with unsaved changes (Design System §33.4)
    /// </summary>
    public interface IHasUnsavedChanges
    {
        bool HasUnsavedChanges { get; }
        string? UnsavedChangesMessage { get; }
    }

    /// <summary>
    /// Interface for ViewModels that return a result
    /// </summary>
    public interface IModalResult<T>
    {
        T? Result { get; }
        bool HasResult { get; }
    }

    /// <summary>
    /// Interface for ViewModels that support initialization parameters
    /// </summary>
    public interface IInitializable<T>
    {
        Task InitializeAsync(T parameter, CancellationToken cancellationToken = default);
    }

    #endregion

    /// <summary>
    /// Production modal navigation service contract
    /// </summary>
    public interface IModalNavigationService : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Gets the current modal stack depth (0-2)
        /// </summary>
        int StackDepth { get; }

        /// <summary>
        /// Gets whether a modal is currently open
        /// </summary>
        bool IsModalOpen { get; }

        /// <summary>
        /// Gets the current modal ViewModel
        /// </summary>
        object? CurrentModalViewModel { get; }

        /// <summary>
        /// Gets whether unsaved changes exist in current modal
        /// </summary>
        bool HasUnsavedChanges { get; }

        /// <summary>
        /// Opens a modal with the specified ViewModel type
        /// </summary>
        /// <typeparam name="TViewModel">Modal ViewModel type</typeparam>
        /// <param name="size">Modal size (overrides ViewModel preference)</param>
        /// <param name="parameter">Initialization parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if modal opened successfully</returns>
        Task<bool> OpenModalAsync<TViewModel>(
            ModalSize? size = null,
            object? parameter = null,
            CancellationToken cancellationToken = default) where TViewModel : class;

        /// <summary>
        /// Opens a modal and returns a result
        /// </summary>
        /// <typeparam name="TViewModel">Modal ViewModel type (must implement IModalResult<T>)</typeparam>
        /// <typeparam name="TResult">Result type</typeparam>
        /// <param name="size">Modal size</param>
        /// <param name="parameter">Initialization parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Modal result or default if cancelled</returns>
        Task<TResult?> OpenModalWithResultAsync<TViewModel, TResult>(
            ModalSize? size = null,
            object? parameter = null,
            CancellationToken cancellationToken = default) where TViewModel : class;

        /// <summary>
        /// Closes the current modal
        /// </summary>
        /// <param name="force">Force close bypassing unsaved changes check</param>
        /// <returns>True if modal closed</returns>
        Task<bool> CloseCurrentModalAsync(bool force = false);

        /// <summary>
        /// Closes all modals
        /// </summary>
        /// <param name="force">Force close bypassing unsaved changes check</param>
        /// <returns>True if all modals closed</returns>
        Task<bool> CloseAllModalsAsync(bool force = false);

        /// <summary>
        /// Shows the unsaved changes dialog (Design System §33.4)
        /// </summary>
        Task<bool> ShowUnsavedChangesDialogAsync();

        /// <summary>
        /// Handles Escape key press (Design System §33.3)
        /// </summary>
        /// <returns>True if Escape was handled</returns>
        Task<bool> HandleEscapeKeyAsync();
    }

    /// <summary>
    /// View mapping service contract
    /// </summary>
    public interface IViewMappingService
    {
        /// <summary>
        /// Registers a View-ViewModel mapping
        /// </summary>
        void Register<TViewModel, TView>() where TView : Window where TViewModel : class;

        /// <summary>
        /// Gets the View type for a ViewModel type
        /// </summary>
        Type GetViewType<TViewModel>() where TViewModel : class;

        /// <summary>
        /// Gets the View type for a ViewModel type
        /// </summary>
        Type GetViewType(Type viewModelType);

        /// <summary>
        /// Creates a View instance for a ViewModel
        /// </summary>
        Window CreateView<TViewModel>(TViewModel viewModel) where TViewModel : class;

        /// <summary>
        /// Creates a View instance for a ViewModel
        /// </summary>
        Window CreateView(Type viewModelType, object viewModel);
    }
}