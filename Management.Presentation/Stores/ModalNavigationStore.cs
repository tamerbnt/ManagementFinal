using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Management.Presentation.Extensions;
using Management.Application.Interfaces.ViewModels;
using Management.Presentation.Services;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.Stores
{
    public class ModalResult
    {
        public bool IsSuccess { get; set; }
        public object? Data { get; set; }
        public string Message { get; set; } = string.Empty;

        public static ModalResult Success(object? data = null)
            => new ModalResult { IsSuccess = true, Data = data };

        public static ModalResult Cancel()
            => new ModalResult { IsSuccess = false };

        public static ModalResult Failure(string message)
            => new ModalResult { IsSuccess = false, Message = message };
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

    public class ModalNavigationStore : ViewModelBase, IDisposable, Management.Domain.Interfaces.IStateResettable
    {
        public void ResetState()
        {
            while (_modalStack.Count > 0)
            {
                var context = _modalStack.Pop();
                DisposeViewModel(context.ViewModel);
                context.CompletionSource.TrySetCanceled();
            }
            CurrentModalViewModel = null;
        }
        private readonly SemaphoreSlim _modalLock = new(1, 1);
        private readonly Stack<ModalContext> _modalStack = new();
        private new readonly ILogger<ModalNavigationStore>? _logger;
        private readonly IServiceProvider _serviceProvider;
        private bool _isDisposed;
        private CancellationTokenSource? _currentOperationCts;

        private object? _currentModalViewModel;
        public object? CurrentModalViewModel
        {
            get => _currentModalViewModel;
            private set
            {
                SetProperty(ref _currentModalViewModel, value);
                OnPropertyChanged(nameof(IsOpen));
                OnPropertyChanged(nameof(ModalCount));
            }
        }

        public bool IsOpen => CurrentModalViewModel != null;
        public int ModalCount => _modalStack.Count;

        public event EventHandler<ModalEventArgs>? ModalOpened;
        public event EventHandler<ModalEventArgs>? ModalClosed;
        public event EventHandler<ModalErrorEventArgs>? ModalError;

        public ModalNavigationStore(
            IServiceProvider serviceProvider,
            ILogger<ModalNavigationStore>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;
        }

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

                var viewModel = _serviceProvider.GetRequiredService<TViewModel>();

                var context = new ModalContext
                {
                    ViewModelType = typeof(TViewModel),
                    ViewModel = viewModel,
                    Parameter = parameter,
                    OpenedAt = DateTime.UtcNow,
                    CompletionSource = new TaskCompletionSource<ModalResult>()
                };

                // PHASED INITIALIZATION (Mirroring ModalNavigationService)
                if (parameter != null)
                {
                    if (viewModel is IParameterReceiver parameterReceiver)
                    {
                        await parameterReceiver.SetParameterAsync(parameter);
                    }

                    if (viewModel is IInitializable<object?> initializable)
                    {
                        await initializable.InitializeAsync(parameter, token);
                    }
                }

                if (viewModel is IModalAware modalAware)
                {
                    // FIX 6: Show the modal FIRST so the user sees it immediately,
                    // then load data. Prevents UI appearing frozen during data fetch.
                    _modalStack.Push(context);
                    CurrentModalViewModel = viewModel;

                    _logger?.LogInformation("Modal opened: {ViewModelType}", typeof(TViewModel).Name);
                    ModalOpened?.Invoke(this, new ModalEventArgs(typeof(TViewModel).Name));

                    // FIX: Release lock BEFORE awaiting to allow CloseAsync to acquire it
                    _modalLock.Release();

                    await modalAware.OnModalOpenedAsync(parameter ?? new object(), token);
                }
                else
                {
                    _modalStack.Push(context);
                    CurrentModalViewModel = viewModel;

                    _logger?.LogInformation("Modal opened: {ViewModelType}", typeof(TViewModel).Name);
                    ModalOpened?.Invoke(this, new ModalEventArgs(typeof(TViewModel).Name));

                    // FIX: Release lock BEFORE awaiting completion
                    _modalLock.Release();
                }

                return await context.CompletionSource.Task;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open modal: {ViewModelType}", typeof(TViewModel).Name);
                ModalError?.Invoke(this, new ModalErrorEventArgs(typeof(TViewModel).Name, ex));
                
                // Ensure lock is released if we failed before the intended release
                if (_modalLock.CurrentCount == 0) _modalLock.Release();
                
                return ModalResult.Failure(ex.Message);
            }
        }

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
                    Parameter = null,
                    OpenedAt = DateTime.UtcNow,
                    CompletionSource = new TaskCompletionSource<ModalResult>(),
                    ViewModel = viewModelInstance
                };

                _modalStack.Push(context);
                CurrentModalViewModel = viewModelInstance;

                _logger?.LogInformation("Modal opened (instance): {ViewModelType}", typeof(TViewModel).Name);
                ModalOpened?.Invoke(this, new ModalEventArgs(typeof(TViewModel).Name));

                // FIX: Release lock BEFORE awaiting completion to allow CloseAsync to acquire it
                _modalLock.Release();
                return await context.CompletionSource.Task;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open modal (instance): {ViewModelType}", typeof(TViewModel).Name);
                ModalError?.Invoke(this, new ModalErrorEventArgs(typeof(TViewModel).Name, ex));

                // Ensure lock is released if we failed before the intended release
                if (_modalLock.CurrentCount == 0) _modalLock.Release();

                return ModalResult.Failure(ex.Message);
            }
        }

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
                await CloseAsync(ModalResult.Cancel(), cancellationToken);
            }
        }

        public void Close()
        {
            _ = CloseAsync(ModalResult.Cancel());
        }

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

        private class ModalContext
        {
            public required Type ViewModelType { get; set; }
            public required object ViewModel { get; set; }
            public object? Parameter { get; set; }
            public DateTime OpenedAt { get; set; }
            public required TaskCompletionSource<ModalResult> CompletionSource { get; set; }
        }
    }
}
