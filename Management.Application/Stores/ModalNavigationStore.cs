using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // ADD THIS

namespace Management.Application.Stores
{
    // ... (IModalAware, IModalClosingValidation, ModalResult remain unchanged) ...

    // =========================================================================
    // 1. SUPPORTING TYPES (Must be defined here to be visible)
    // =========================================================================

    /// <summary>
    /// Represents the outcome of a modal dialog (Success/Cancel + Data).
    /// </summary>
    public class ModalResult
    {
        public bool IsSuccess { get; set; }
        public object Data { get; set; }
        public string Message { get; set; }

        public static ModalResult Success(object data = null)
            => new ModalResult { IsSuccess = true, Data = data };

        public static ModalResult Cancel()
            => new ModalResult { IsSuccess = false };

        public static ModalResult Failure(string message)
            => new ModalResult { IsSuccess = false, Message = message };
    }

    /// <summary>
    /// Interface for ViewModels that need to accept parameters when opened.
    /// </summary>
    public interface IModalAware
    {
        Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for ViewModels that need to validate closing (e.g. Unsaved Changes).
    /// </summary>
    public interface IModalClosingValidation
    {
        Task<bool> CanCloseAsync(CancellationToken cancellationToken = default);
    }

    public class ModalEventArgs : EventArgs
    {
        public string ModalName { get; }
        public ModalEventArgs(string modalName) => ModalName = modalName;
    }

    public class ModalErrorEventArgs : EventArgs
    {
        public string ModalName { get; }
        public Exception Error { get; }
        public ModalErrorEventArgs(string modalName, Exception error)
        {
            ModalName = modalName;
            Error = error;
        }
    }

    // =========================================================================
    // 2. THE STORE IMPLEMENTATION
    // =========================================================================

    public class ModalNavigationStore : ObservableObject, IDisposable
    {
        #region Private Fields

        private readonly SemaphoreSlim _modalLock = new(1, 1);
        private readonly Stack<ModalContext> _modalStack = new();
        private readonly ILogger<ModalNavigationStore> _logger;
        private readonly IServiceProvider _serviceProvider; // ADD THIS FIELD
        private bool _isDisposed;
        private CancellationTokenSource _currentOperationCts;

        #endregion

        #region Public Properties

        private object? _currentModalViewModel;
        public object? CurrentModalViewModel
        {
            get => _currentModalViewModel;
            private set // Keep private setter, we'll update it from the internal Open/CloseAsync methods
            {
                SetProperty(ref _currentModalViewModel, value);
                OnPropertyChanged(nameof(IsOpen));
                OnPropertyChanged(nameof(ModalCount));
            }
        }

        public bool IsOpen => CurrentModalViewModel != null;
        public int ModalCount => _modalStack.Count;

        #endregion

        #region Events

        public event EventHandler<ModalEventArgs>? ModalOpened;
        public event EventHandler<ModalEventArgs>? ModalClosed;
        public event EventHandler<ModalErrorEventArgs>? ModalError;

        #endregion

        #region Constructor

        public ModalNavigationStore(
            IServiceProvider serviceProvider, // ADD THIS PARAMETER
            ILogger<ModalNavigationStore> logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)); // ASSIGN IT
            _logger = logger;
        }

        #endregion

        #region Open Modal

        // This is the primary OpenAsync method
        public async Task<ModalResult> OpenAsync<TViewModel>(
            object? parameter = null,
            CancellationToken cancellationToken = default)
            where TViewModel : class
        {
            ThrowIfDisposed();

            await _modalLock.WaitAsync(cancellationToken);
            try
            {
                _currentOperationCts?.Cancel();
                _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = _currentOperationCts.Token;

                var context = new ModalContext
                {
                    ViewModelType = typeof(TViewModel),
                    Parameter = parameter,
                    OpenedAt = DateTime.UtcNow,
                    CompletionSource = new TaskCompletionSource<ModalResult>()
                };

                // ✅ Use DI to create ViewModel
                var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
                context.ViewModel = viewModel;

                // Initialize with parameter
                if (viewModel is IModalAware modalAware && parameter != null)
                {
                    await modalAware.OnModalOpenedAsync(parameter, token);
                }

                // Push to stack and update UI
                _modalStack.Push(context);
                CurrentModalViewModel = viewModel; // Update public property

                _logger?.LogInformation("Modal opened: {ViewModelType}", typeof(TViewModel).Name);
                ModalOpened?.Invoke(this, new ModalEventArgs(typeof(TViewModel).Name));

                return await context.CompletionSource.Task;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open modal: {ViewModelType}", typeof(TViewModel).Name);
                ModalError?.Invoke(this, new ModalErrorEventArgs(typeof(TViewModel).Name, ex));
                return ModalResult.Failure(ex.Message);
            }
            finally
            {
                _modalLock.Release();
            }
        }

        // Keep this overload for pre-created instances, but DialogService will use the above.
        public async Task<ModalResult> OpenAsync<TViewModel>(
            TViewModel viewModelInstance,
            CancellationToken cancellationToken = default)
            where TViewModel : class
        {
            ThrowIfDisposed();

            await _modalLock.WaitAsync(cancellationToken);
            try
            {
                _currentOperationCts?.Cancel();
                _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = _currentOperationCts.Token;

                var context = new ModalContext
                {
                    ViewModelType = typeof(TViewModel),
                    Parameter = null, // Instance is already configured
                    OpenedAt = DateTime.UtcNow,
                    CompletionSource = new TaskCompletionSource<ModalResult>(),
                    ViewModel = viewModelInstance
                };

                // Push to stack and update UI
                _modalStack.Push(context);
                CurrentModalViewModel = viewModelInstance;

                _logger?.LogInformation("Modal opened (instance): {ViewModelType}", typeof(TViewModel).Name);
                ModalOpened?.Invoke(this, new ModalEventArgs(typeof(TViewModel).Name));

                return await context.CompletionSource.Task;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open modal (instance): {ViewModelType}", typeof(TViewModel).Name);
                ModalError?.Invoke(this, new ModalErrorEventArgs(typeof(TViewModel).Name, ex));
                return ModalResult.Failure(ex.Message);
            }
            finally
            {
                _modalLock.Release();
            }
        }

        #endregion

        #region Close Modal

        public async Task<bool> CloseAsync(
            ModalResult? result = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsOpen) return false;

            await _modalLock.WaitAsync(cancellationToken);
            try
            {
                var context = _modalStack.Peek();

                // Validate if can close
                if (context.ViewModel is IModalClosingValidation validation)
                {
                    var canClose = await validation.CanCloseAsync(cancellationToken);
                    if (!canClose)
                    {
                        _logger?.LogInformation("Modal close cancelled by validation");
                        return false;
                    }
                }

                _modalStack.Pop();
                DisposeViewModel(context.ViewModel);
                context.CompletionSource.TrySetResult(result ?? ModalResult.Cancel());

                CurrentModalViewModel = _modalStack.Count > 0 ? _modalStack.Peek().ViewModel : null;

                _logger?.LogInformation("Modal closed: {ViewModelType}", context.ViewModelType.Name);
                ModalClosed?.Invoke(this, new ModalEventArgs(context.ViewModelType.Name));

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to close modal");
                ModalError?.Invoke(this, new ModalErrorEventArgs("Unknown", ex));
                return false;
            }
            finally
            {
                _modalLock.Release();
            }
        }

        public async Task CloseAllAsync(CancellationToken cancellationToken = default)
        {
            while (IsOpen)
            {
                // Force close (no validation)
                await CloseAsync(ModalResult.Cancel(), cancellationToken);
            }
        }

        /// <summary>
        /// Synchronous wrapper for CloseAsync.
        /// Used by simple consumers (like DialogService callbacks) that don't need to await the result.
        /// </summary>
        public void Close()
        {
            // Fire and forget closure with default Cancel result
            _ = CloseAsync(ModalResult.Cancel());
        }

        #endregion

        #region Helper Methods

        // Remove the old CreateViewModel method since we are using DI
        // private TViewModel CreateViewModel<TViewModel>() where TViewModel : class { ... }

        private void DisposeViewModel(object viewModel)
        {
            if (viewModel is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                    _logger?.LogDebug("ViewModel disposed: {Type}", viewModel.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to dispose ViewModel: {Type}", viewModel.GetType().Name);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(ModalNavigationStore));
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _currentOperationCts?.Cancel();
            _currentOperationCts?.Dispose();

            while (_modalStack.Count > 0)
            {
                var context = _modalStack.Pop();
                DisposeViewModel(context.ViewModel);
                context.CompletionSource.TrySetCanceled();
            }

            _modalLock?.Dispose();
            _logger?.LogInformation("ModalNavigationStore disposed");
        }

        #endregion

        #region Supporting Types

        private class ModalContext
        {
            public Type ViewModelType { get; set; }
            public object ViewModel { get; set; }
            public object? Parameter { get; set; }
            public DateTime OpenedAt { get; set; }
            public TaskCompletionSource<ModalResult> CompletionSource { get; set; }
        }

        #endregion
    }

}

    #region Event Args

    public class ModalEventArgs : EventArgs
    {
        public string ModalName { get; }
        public ModalEventArgs(string modalName) => ModalName = modalName;
    }

    public class ModalErrorEventArgs : EventArgs
    {
        public string ModalName { get; }
        public Exception Error { get; }
        public ModalErrorEventArgs(string modalName, Exception error)
        {
            ModalName = modalName;
            Error = error;
        }
    }

    #endregion

