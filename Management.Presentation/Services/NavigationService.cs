// ******************************************************************************************
//  Management.Presentation/Services/NavigationService.cs
//  FINAL PRODUCTION VERSION – v1.3.2-detail-support
//  Design System: Apple 2025 Edition – v1.2 FINAL
//  Status: PRODUCTION READY
// ******************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Presentation.ViewModels;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.Services
{
    #region Public Interfaces & Contracts

    /// <summary>
    /// Implemented by ViewModels that need to receive data during navigation (e.g., Detail Views receiving an ID).
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// Called immediately after the ViewModel is created but before it is set as Current.
        /// </summary>
        /// <param name="parameter">The data passed from the navigation source.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task OnNavigatedToAsync(object parameter, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Optional interface for view models that require async initialization logic (loading data) 
    /// before being displayed.
    /// </summary>
    public interface IAsyncInitialization
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Clean navigation contract - signals navigation state, view handles animations.
    /// </summary>
    public interface INavigationService : INotifyPropertyChanged, IDisposable
    {
        object? CurrentViewModel { get; }
        int CurrentScreenIndex { get; }
        bool IsNavigating { get; }
        IReadOnlyList<string> ScreenTitles { get; }

        /// <summary>
        /// Navigate to one of the 8 root screens (0 = Dashboard, 7 = Settings).
        /// Used by the Sidebar.
        /// </summary>
        Task<bool> NavigateToAsync(int screenIndex, CancellationToken cancellationToken = default);



        /// <summary>
        /// Navigate using keyboard shortcut (Ctrl+1-8).
        /// </summary>
        void NavigateWithShortcut(int shortcutNumber);
    }

    #endregion

    #region Supporting Types (Events & Enums)

    public class NavigationEventArgs : EventArgs
    {
        public int ScreenIndex { get; }
        public string ScreenName { get; }
        public bool IsSuccessful { get; }
        public Exception? Error { get; }

        public NavigationEventArgs(int screenIndex, string screenName, bool isSuccessful, Exception? error = null)
        {
            ScreenIndex = screenIndex;
            ScreenName = screenName;
            IsSuccessful = isSuccessful;
            Error = error;
        }
    }

    public enum NavigationEventType { Started, Completed, Cancelled, Failed }

    public enum NavigationScreen
    {
        Dashboard = 0,
        AccessControl = 1,
        Members = 2,
        Registrations = 3,
        History = 4,
        FinanceAndStaff = 5,
        Shop = 6,
        Settings = 7,
        DetailView = -1 // Represents a view not on the sidebar
    }

    /// <summary>
    /// Dispatcher abstraction for testability
    /// </summary>
    public interface IDispatcher
    {
        bool CheckAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        Task<T> InvokeAsync<T>(Func<T> func);
        Task InvokeAsync(Action action, DispatcherPriority priority);
    }

    #endregion

    /// <summary>
    /// Production NavigationService.
    /// Handles Lifecycle, Threading, Parameter Passing, and Logging.
    /// </summary>
    public sealed class NavigationService : ObservableObject, INavigationService
    {
        #region Constants & Static Data

        // Fixed 8-screen contract for Sidebar
        private static readonly IReadOnlyDictionary<int, Type> ScreenViewModels = new Dictionary<int, Type>
        {
            { 0, typeof(DashboardViewModel) },
            { 1, typeof(AccessControlViewModel) },
            { 2, typeof(MembersViewModel) },
            { 3, typeof(RegistrationsViewModel) },
            { 4, typeof(HistoryViewModel) },
            { 5, typeof(FinanceAndStaffViewModel) },
            { 6, typeof(ShopViewModel) },
            { 7, typeof(SettingsViewModel) }
        };

        public static IReadOnlyList<string> ScreenTitles { get; } = new List<string>
        {
            "Dashboard", "Access Control", "Members", "Registrations",
            "History", "Finance & Staff", "Shop", "Settings"
        }.AsReadOnly();

        private const int NoScreenIndex = -1; // Used when navigating to a Detail View

        #endregion

        #region Private Fields

        private readonly Func<Type, object> _viewModelFactory;
        private readonly IDispatcher _dispatcher;
        private readonly ILogger<NavigationService>? _logger;

        private object? _currentViewModel;
        private int _currentScreenIndex = -1;
        private bool _isNavigating;
        private bool _isDisposed;

        // Thread-safe navigation state
        private readonly SemaphoreSlim _navigationLock = new(1, 1);
        private CancellationTokenSource? _currentNavigationCts;

        // Events
        public event EventHandler<NavigationEventArgs>? NavigationStarted;
        public event EventHandler<NavigationEventArgs>? NavigationCompleted;
        public event EventHandler<NavigationEventArgs>? NavigationFailed;

        #endregion

        #region Public Properties

        public object? CurrentViewModel => _currentViewModel;
        public int CurrentScreenIndex => _currentScreenIndex;
        public bool IsNavigating => _isNavigating;
        IReadOnlyList<string> INavigationService.ScreenTitles => ScreenTitles;

        #endregion

        #region Constructor

        public NavigationService(
            Func<Type, object> viewModelFactory,
            IDispatcher dispatcher,
            ILogger<NavigationService>? logger = null)
        {
            _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _logger = logger;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Standard Sidebar Navigation (Index 0-7).
        /// </summary>
        public async Task<bool> NavigateToAsync(int screenIndex, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsValidScreenIndex(screenIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(screenIndex),
                    $"Screen index must be between 0 and {ScreenViewModels.Count - 1}.");
            }

            if (screenIndex == CurrentScreenIndex)
            {
                LogNavigationEvent(NavigationEventType.Completed, screenIndex, "Already on target screen");
                return true;
            }

            await _navigationLock.WaitAsync(cancellationToken);
            try
            {
                var viewModelType = ScreenViewModels[screenIndex];
                var screenName = ScreenTitles[screenIndex];

                // Perform navigation, updating the index
                return await ExecuteNavigationCoreAsync(
                    viewModelType,
                    screenName,
                    screenIndex,
                    parameter: null,
                    cancellationToken
                );
            }
            finally
            {
                _navigationLock.Release();
            }
        }

        /// <summary>
        /// Parameterized Detail Navigation (Drill-down).
        /// Sets ScreenIndex to -1 to deselect sidebar.
        /// </summary>
        public async Task<bool> NavigateToDetailAsync<TViewModel>(object parameter, CancellationToken cancellationToken = default)
            where TViewModel : class
        {
            ThrowIfDisposed();

            await _navigationLock.WaitAsync(cancellationToken);
            try
            {
                var type = typeof(TViewModel);
                var screenName = type.Name.Replace("ViewModel", ""); // e.g. "MemberDetail"

                // Perform navigation, setting index to -1 (No Sidebar Item)
                return await ExecuteNavigationCoreAsync(
                    type,
                    screenName,
                    NoScreenIndex,
                    parameter,
                    cancellationToken
                );
            }
            finally
            {
                _navigationLock.Release();
            }
        }

        public void NavigateWithShortcut(int shortcutNumber)
        {
            if (shortcutNumber < 1 || shortcutNumber > ScreenViewModels.Count)
            {
                _logger?.LogWarning("Invalid keyboard shortcut: {ShortcutNumber}", shortcutNumber);
                return;
            }

            var screenIndex = shortcutNumber - 1; // Convert 1-8 to 0-7

            // Fire and forget
            _ = NavigateToAsync(screenIndex).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _logger?.LogError(task.Exception, "Keyboard shortcut navigation failed for Ctrl+{ShortcutNumber}", shortcutNumber);
                }
            }, TaskScheduler.Default);
        }

        #endregion

        #region Core Implementation

        /// <summary>
        /// The unified logic engine for all navigation types.
        /// </summary>
        private async Task<bool> ExecuteNavigationCoreAsync(
            Type viewModelType,
            string screenName,
            int screenIndex,
            object? parameter,
            CancellationToken cancellationToken)
        {
            CancelCurrentNavigation();
            _currentNavigationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _currentNavigationCts.Token;

            try
            {
                // 1. Signal Start
                await _dispatcher.InvokeAsync(() =>
                {
                    _isNavigating = true;
                    OnPropertyChanged(nameof(IsNavigating));
                });

                LogNavigationEvent(NavigationEventType.Started, screenIndex, screenName);
                OnNavigationStarted(new NavigationEventArgs(screenIndex, screenName, false));

                // 2. Factory Creation
                var viewModel = await CreateViewModelAsync(viewModelType, token);

                // 3. Parameter Injection (INavigationAware)
                if (viewModel is INavigationAware navAware && parameter != null)
                {
                    await navAware.OnNavigatedToAsync(parameter, token);
                }

                // 4. Async Initialization (IAsyncInitialization)
                if (viewModel is IAsyncInitialization asyncInit)
                {
                    await asyncInit.InitializeAsync(token);
                }

                // 5. Update UI State & Dispose Old VM
                await UpdateUIStateAsync(viewModel, screenIndex, token);

                // 6. Signal Completion
                await _dispatcher.InvokeAsync(() =>
                {
                    _isNavigating = false;
                    OnPropertyChanged(nameof(IsNavigating));
                });

                LogNavigationEvent(NavigationEventType.Completed, screenIndex, screenName);
                OnNavigationCompleted(new NavigationEventArgs(screenIndex, screenName, true));

                return true;
            }
            catch (OperationCanceledException)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _isNavigating = false;
                    OnPropertyChanged(nameof(IsNavigating));
                });
                LogNavigationEvent(NavigationEventType.Cancelled, screenIndex);
                return false;
            }
            catch (Exception ex)
            {
                await HandleNavigationErrorAsync(screenIndex, ex);
                return false;
            }
            finally
            {
                _currentNavigationCts?.Dispose();
                _currentNavigationCts = null;
            }
        }

        private async Task<object> CreateViewModelAsync(Type viewModelType, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Run factory on thread pool to avoid blocking UI during heavy constructor injection
            var viewModel = await Task.Run(() => _viewModelFactory(viewModelType), cancellationToken);

            if (viewModel == null)
            {
                throw new InvalidOperationException($"ViewModel factory returned null for type {viewModelType.Name}");
            }

            return viewModel;
        }

        private async Task UpdateUIStateAsync(object viewModel, int screenIndex, CancellationToken cancellationToken)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested) return;

                // Atomic swap and dispose
                var previousViewModel = Interlocked.Exchange(ref _currentViewModel, viewModel);
                if (previousViewModel is IDisposable disposable)
                {
                    SafeDispose(disposable, previousViewModel.GetType().Name);
                }

                _currentScreenIndex = screenIndex;

                // Accessibility Focus Reset
                if (SystemParameters.HighContrast) Keyboard.ClearFocus();
                else Keyboard.Focus(null);

                OnPropertyChanged(nameof(CurrentViewModel));
                OnPropertyChanged(nameof(CurrentScreenIndex));
            }, DispatcherPriority.DataBind);
        }

        private async Task HandleNavigationErrorAsync(int screenIndex, Exception exception)
        {
            var screenName = screenIndex >= 0 ? ScreenTitles[screenIndex] : "DetailView";

            LogNavigationEvent(NavigationEventType.Failed, screenIndex, screenName, exception);
            OnNavigationFailed(new NavigationEventArgs(screenIndex, screenName, false, exception));

            await _dispatcher.InvokeAsync(() =>
            {
                _isNavigating = false;
                OnPropertyChanged(nameof(IsNavigating));
            });
        }

        private void CancelCurrentNavigation()
        {
            _currentNavigationCts?.Cancel();
        }

        #endregion

        #region Helpers & Disposal

        private bool IsValidScreenIndex(int screenIndex)
        {
            return screenIndex >= 0 && screenIndex < ScreenViewModels.Count;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(NavigationService));
        }

        private void SafeDispose(IDisposable disposable, string typeName)
        {
            try { disposable.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Failed to dispose {TypeName}", typeName); }
        }

        private void LogNavigationEvent(NavigationEventType eventType, int screenIndex, string? screenName = null, Exception? exception = null)
        {
            if (_logger == null) return;
            var name = screenName ?? (screenIndex >= 0 ? ((NavigationScreen)screenIndex).ToString() : "Detail");

            switch (eventType)
            {
                case NavigationEventType.Started: _logger.LogInformation("Nav started: {Screen}", name); break;
                case NavigationEventType.Completed: _logger.LogInformation("Nav completed: {Screen}", name); break;
                case NavigationEventType.Cancelled: _logger.LogDebug("Nav cancelled: {Screen}", name); break;
                case NavigationEventType.Failed: _logger.LogError(exception, "Nav failed: {Screen}", name); break;
            }
        }

        private void OnNavigationStarted(NavigationEventArgs e) => NavigationStarted?.Invoke(this, e);
        private void OnNavigationCompleted(NavigationEventArgs e) => NavigationCompleted?.Invoke(this, e);
        private void OnNavigationFailed(NavigationEventArgs e) => NavigationFailed?.Invoke(this, e);

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_dispatcher.CheckAccess()) base.OnPropertyChanged(e);
            else _dispatcher.InvokeAsync(() => base.OnPropertyChanged(e));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            lock (this) { if (_isDisposed) return; _isDisposed = true; }

            CancelCurrentNavigation();
            if (_currentViewModel is IDisposable disposable) SafeDispose(disposable, _currentViewModel.GetType().Name);

            _currentViewModel = null;
            _currentNavigationCts?.Dispose();
            _navigationLock?.Dispose();

            _logger?.LogInformation("NavigationService disposed");
        }

        #endregion
    }
}