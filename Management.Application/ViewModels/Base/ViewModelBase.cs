using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces.ViewModels;
using System.Threading;

using Management.Domain.Interfaces;

namespace Management.Application.ViewModels.Base
{
    public abstract partial class ViewModelBase : ObservableObject, IDisposable, IStateResettable, IModalAware
    {
        // Concurrency guards to prevent double-execution of load/save operations
        private readonly SemaphoreSlim _loadingLock = new(1, 1);
        private readonly SemaphoreSlim _executionLock = new(1, 1);
        public virtual void ResetState()
        {
            // Default implementation for Singletons to clear transient state
            IsBusy = false;
            IsLoading = false;
            HasError = false;
            ErrorMessage = null;
        }
        // Used to disable buttons/interactions (Input Lock)
        [ObservableProperty]
        private bool _isBusy;

        // Used to show Skeleton Shimmers (Visual State)
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _title = string.Empty;

        protected readonly ILogger? _logger;
        protected readonly IDiagnosticService? _diagnosticService;
        protected readonly IToastService? _toastService;
        protected bool _isDisposed;

        // ── Sync Debounce ─────────────────────────────────────────────────────────
        // One process-wide cooldown shared across all ViewModel instances.
        // Prevents a sync storm when SyncCompleted fires and multiple VMs respond.
        private static DateTime _lastSyncRefresh = DateTime.MinValue;
        private static readonly TimeSpan SyncDebounceWindow = TimeSpan.FromSeconds(3);

        /// <summary>
        /// True when this ViewModel is the currently visible/active screen.
        /// Set to true in the ViewModel load entry-point and false in ResetState/Dispose.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Returns true only if: (a) this ViewModel is the active screen, AND
        /// (b) at least 3 seconds have passed since any sync-triggered refresh.
        /// Updates the shared timestamp on success, blocking other VMs for the window.
        /// </summary>
        protected bool ShouldRefreshOnSync()
        {
            if (!IsActive) return false;
            var now = DateTime.UtcNow;
            if (now - _lastSyncRefresh < SyncDebounceWindow) return false;
            _lastSyncRefresh = now;
            return true;
        }

        public bool IsDisposed => _isDisposed;

        protected ViewModelBase(
            ILogger? logger = null, 
            IDiagnosticService? diagnosticService = null,
            IToastService? toastService = null)
        {
            _logger = logger;
            _diagnosticService = diagnosticService;
            _toastService = toastService;
        }

        protected virtual void OnError(Exception ex, string msg)
        {
            _logger?.LogError(ex, "ViewModel Error: {Message}", msg);
            _diagnosticService?.Track(ex, context: $"{Title}: {msg}");
            HasError = true;
            ErrorMessage = msg;
            _toastService?.ShowError(msg, Title);
        }

        protected void ShowError(string message)
        {
            _toastService?.ShowError(message, Title);
        }

        /// <summary>
        /// Use for USER ACTIONS (Saving, Deleting). Locks the UI via IsBusy.
        /// Enforces single-execution concurrency.
        /// </summary>
        protected async Task ExecuteSafeAsync(Func<Task> action, string? errorMsg = null)
        {
            if (IsBusy) return; // Quick synchronous check

            // Wait for lock. If already locked, we wait instead of failing silently 
            // to ensure sequential processing of user commands (e.g., fast double-clicks).
            if (_isDisposed) return;
            await _executionLock.WaitAsync(); 
            try
            {
                if (_isDisposed || IsBusy) return; // Double-check after acquiring lock
                
                IsBusy = true;
                HasError = false;
                ErrorMessage = null;
                await action();
            }
            catch (Exception ex)
            {
                OnError(ex, errorMsg ?? ex.Message);
            }
            finally
            {
                IsBusy = false;
                if (!_isDisposed)
                {
                    try { _executionLock.Release(); }
                    catch (ObjectDisposedException) { /* Handle race condition on disposal */ }
                }
            }
        }

        /// <summary>
        /// Use for DATA FETCHING (Navigation, Initial Load). Shows Skeleton Loader via IsLoading.
        /// Enforces single-execution to prevent double-loading on facility/navigation overlap.
        /// </summary>
        protected async Task ExecuteLoadingAsync(Func<Task> action, string? errorMsg = null)
        {
            if (IsLoading) return; // Quick synchronous check

            // If we can't get the lock immediately (0ms), it means another load is actively happening.
            // We just return instead of waiting. This prevents the "double load" when Navigation 
            // and FacilityChanged trigger at the exact same millisecond.
            if (_isDisposed) return;
            if (!await _loadingLock.WaitAsync(0))
            {
                _logger?.LogDebug("[ViewModelBase] ExecuteLoadingAsync aborted: already loading.");
                return;
            }

            try
            {
                IsLoading = true; // Triggers Skeleton View in XAML
                HasError = false;
                ErrorMessage = null;
                await action();
            }
            catch (Exception ex)
            {
                OnError(ex, errorMsg ?? "Failed to load data.");
            }
            finally
            {
                IsLoading = false; // Hides Skeleton, shows Content
                if (!_isDisposed)
                {
                    try { _loadingLock.Release(); }
                    catch (ObjectDisposedException) { /* Handle race condition on disposal */ }
                }
            }
        }

        public virtual Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            _isDisposed = true; // Mark as disposed BEFORE cleaning up resources
            
            if (disposing)
            {
                _loadingLock.Dispose();
                _executionLock.Dispose();
            }
        }
    }
}
