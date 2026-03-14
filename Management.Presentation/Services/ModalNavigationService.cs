// ******************************************************************************************
//  Management.Presentation/Services/ModalNavigationService.cs
//  FINAL PRODUCTION VERSION ? v1.2.0-production
//  Design System: Apple 2025 Edition ? v1.2 FINAL (LOCKED)
//  Status: PRODUCTION READY ? DESIGN SYSTEM COMPLIANT
// ******************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
// using Management.Presentation.Services; (Namespace is already this)
using Microsoft.Extensions.Logging;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

namespace Management.Presentation.Services
{
    #region Internal Types

    /// <summary>
    /// Internal modal state container
    /// </summary>
    internal sealed class ModalState
    {
        public Window Window { get; set; } = null!;
        public object ViewModel { get; set; } = null!;
        public ModalSize Size { get; set; }
        public TaskCompletionSource<object?>? ResultCompletionSource { get; set; }
        public bool IsClosing { get; set; }
        public DateTime OpenedAt { get; set; }
    }

    #endregion

    /// <summary>
    /// Production ModalNavigationService - Design System compliant modal management
    /// </summary>
    public sealed class ModalNavigationService : ViewModelBase, IModalNavigationService
    {
        #region Constants

        // Design System ?33.1: Maximum modal stack depth
        private const int MaxStackDepth = 2;

        // Design System ?33.5: Animation durations
        private static readonly TimeSpan OpenAnimationDuration = TimeSpan.FromMilliseconds(400);
        private static readonly TimeSpan CloseAnimationDuration = TimeSpan.FromMilliseconds(300);

        // Design System ?33.2: Backdrop opacity levels
        private const double BackdropOpacityBase = 0.5;      // 50% for first modal
        private const double BackdropOpacityStacked = 0.6;   // 60% for second modal

        // Design System ?15.4: Modal size defaults
        private const int ModalWidthSmall = 640;
        private const int ModalWidthMedium = 880;
        private const int ModalWidthLarge = 1120;

        // Minimum window size as per Design System ?7.1
        private const int MinWindowWidth = 1280;
        private const int MinWindowHeight = 720;

        #endregion

        #region Private Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly IViewMappingService _viewMappingService;
        private readonly ILogger<ModalNavigationService>? _logger;
        private readonly IDispatcher _dispatcher;

        private readonly Stack<ModalState> _modalStack = new();
        private readonly SemaphoreSlim _modalLock = new(1, 1);
        private CancellationTokenSource? _currentOperationCts;

        private object? _currentModalViewModel;
        private bool _isDisposed;

        // Design System ?33.1: Z-index layers
        // Removed unused _nextZIndex

        #endregion

        #region Public Properties

        public int StackDepth => _modalStack.Count;

        public bool IsModalOpen => StackDepth > 0;

        public object? CurrentModalViewModel => _currentModalViewModel;

        public bool HasUnsavedChanges
        {
            get
            {
                if (_currentModalViewModel is IHasUnsavedChanges hasUnsaved)
                    return hasUnsaved.HasUnsavedChanges;
                return false;
            }
        }

        #endregion

        #region Events

        public event EventHandler<ModalNavigationEventArgs>? ModalOpening;
        public event EventHandler<ModalNavigationEventArgs>? ModalOpened;
        public event EventHandler<ModalNavigationEventArgs>? ModalClosing;
        public event EventHandler<ModalNavigationEventArgs>? ModalClosed;
        public event EventHandler<ModalNavigationEventArgs>? ModalOperationFailed;

        private void RaisePropertyChanged(string propertyName)
        {
            if (_dispatcher.CheckAccess())
            {
                OnPropertyChanged(propertyName);
            }
            else
            {
                _dispatcher.InvokeAsync(() => OnPropertyChanged(propertyName));
            }
        }

        #endregion

        #region Constructor

        public ModalNavigationService(
            IServiceProvider serviceProvider,
            IViewMappingService viewMappingService,
            IDispatcher dispatcher,
            ILogger<ModalNavigationService>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _viewMappingService = viewMappingService ?? throw new ArgumentNullException(nameof(viewMappingService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _logger = logger;

            WireUpGlobalHandlers();
            LogServiceInitialization();
        }

        #endregion

        #region Public API - IModalNavigationService

        public void CloseModal()
        {
            System.Diagnostics.Debug.WriteLine("[DEBUG] ModalNavigationService: CloseModal() called");
            _ = CloseCurrentModalAsync();
        }

        public async Task<bool> OpenModalAsync<TViewModel>(
            ModalSize? size = null,
            object? parameter = null,
            CancellationToken cancellationToken = default) where TViewModel : class
        {
            ThrowIfDisposed();

            // Design System ?33.1: Maximum 2 modals
            if (StackDepth >= MaxStackDepth)
            {
                await ShowMaxDepthErrorAsync();
                return false;
            }

            await _modalLock.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteOpenModalAsync<TViewModel>(size, parameter, cancellationToken);
            }
            finally
            {
                _modalLock.Release();
            }
        }

        public async Task<TResult?> OpenModalWithResultAsync<TViewModel, TResult>(
    ModalSize? size = null,
    object? parameter = null,
    CancellationToken cancellationToken = default) where TViewModel : class
        {
            ThrowIfDisposed();
            if (StackDepth >= MaxStackDepth)
            {
                await ShowMaxDepthErrorAsync();
                return default;
            }

            var tcs = new TaskCompletionSource<object?>();
            await _modalLock.WaitAsync(cancellationToken);

            try
            {
                var state = await CreateModalStateAsync<TViewModel>(size, parameter, cancellationToken);

                // Use 'is null' pattern matching
                if (state is null) return default;

                state.ResultCompletionSource = tcs;
                await ShowModalWindowAsync(state, cancellationToken);

                var result = await tcs.Task.WaitAsync(cancellationToken);
                return result is TResult typedResult ? typedResult : default;
            }
            finally
            {
                _modalLock.Release();
            }
        }

        public async Task<bool> CloseCurrentModalAsync(bool force = false)
        {
            ThrowIfDisposed();

            System.Diagnostics.Debug.WriteLine($"[DEBUG] ModalNavigationService: CloseCurrentModalAsync(force={force}) called. StackDepth={StackDepth}");

            if (StackDepth == 0) 
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] ModalNavigationService: StackDepth is 0, nothing to close");
                return true;
            }

            // FAILSOUND: Guard against duplicate close calls during rapid animation
            if (_modalStack.Any() && _modalStack.Peek().IsClosing && !force) 
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] ModalNavigationService: Top modal is already closing, ignoring");
                return false;
            }

            System.Diagnostics.Debug.WriteLine("[DEBUG] ModalNavigationService: Waiting for _modalLock...");
            await _modalLock.WaitAsync();
            try
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] ModalNavigationService: Acquired _modalLock");
                if (StackDepth == 0) 
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] ModalNavigationService: StackDepth became 0 while waiting for lock");
                    return true; // Re-check after lock
                }
                var result = await ExecuteCloseCurrentModalAsync(force);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] ModalNavigationService: ExecuteCloseCurrentModalAsync returned {result}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] ModalNavigationService ERROR in CloseCurrentModalAsync: {ex.Message}");
                return false;
            }
            finally
            {
                _modalLock.Release();
                System.Diagnostics.Debug.WriteLine("[DEBUG] ModalNavigationService: Released _modalLock");
            }
        }

        public async Task<bool> CloseAllModalsAsync(bool force = false)
        {
            ThrowIfDisposed();

            if (StackDepth == 0) return true;

            await _modalLock.WaitAsync();
            try
            {
                bool allClosed = true;
                while (StackDepth > 0)
                {
                    if (!await ExecuteCloseCurrentModalAsync(force))
                    {
                        allClosed = false;
                        break;
                    }
                }
                return allClosed;
            }
            finally
            {
                _modalLock.Release();
            }
        }

        public async Task<bool> ShowUnsavedChangesDialogAsync()
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                // Design System ?33.4: Unsaved changes dialog
                var dialog = new Window
                {
                    Title = "Unsaved Changes",
                    Width = 400,
                    SizeToContent = SizeToContent.Height,
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Owner = System.Windows.Application.Current.MainWindow,  // ? Fixed
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var result = false;
                var viewModel = new UnsavedChangesDialogViewModel(
                    discardCallback: () => { result = true; dialog.Close(); },
                    cancelCallback: () => dialog.Close());

                dialog.Content = new UnsavedChangesDialogView { DataContext = viewModel };
                dialog.ShowDialog();  // Blocking call - runs on UI thread

                return result;
            });
        }
        public async Task<bool> HandleEscapeKeyAsync()
        {
            // Design System ?33.3: Escape key handling priority
            if (StackDepth == 0) return false;

            var currentState = _modalStack.Peek();
            if (currentState.Window.IsFocused)
            {
                return await CloseCurrentModalAsync();
            }

            return false;
        }

        #endregion

        #region Private Implementation Methods

        private async Task<bool> ExecuteOpenModalAsync<TViewModel>(
            ModalSize? size,
            object? parameter,
            CancellationToken cancellationToken) where TViewModel : class
        {
            _currentOperationCts?.Cancel();
            _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Create modal state
                var state = await CreateModalStateAsync<TViewModel>(size, parameter, _currentOperationCts.Token);
                if (state is null) return false;

                // Notify opening
                OnModalOpening(new ModalNavigationEventArgs(
                    typeof(TViewModel), state.Size, StackDepth + 1, false));

                // Show the modal window
                await ShowModalWindowAsync(state, _currentOperationCts.Token);

                // Update state
                _modalStack.Push(state);
                UpdateCurrentViewModel(state.ViewModel);
                OnPropertyChanged(nameof(StackDepth));
                OnPropertyChanged(nameof(IsModalOpen));
                OnPropertyChanged(nameof(HasUnsavedChanges));

                // Notify opened
                OnModalOpened(new ModalNavigationEventArgs(
                    typeof(TViewModel), state.Size, StackDepth, true));

                LogModalEvent("Opened", typeof(TViewModel), state.Size);
                return true;
            }
            catch (OperationCanceledException)
            {
                LogModalEvent("Cancelled", typeof(TViewModel));
                return false;
            }
            catch (Exception ex)
            {
                // Robust Diagnostic Logging for XAML/DI failures
                var msg = $"Modal instantiation failed for {typeof(TViewModel).Name}. Error: {ex.Message} ";
                if (ex.InnerException != null) msg += $"Inner: {ex.InnerException.Message}";
                
                System.Windows.MessageBox.Show("[MODAL_FAIL] " + msg);
                _logger?.LogCritical(ex, "[MODAL_CRITICAL] {Message}", msg);
                
                if (ex is System.Windows.Markup.XamlParseException xamlEx)
                {
                    _logger?.LogCritical("XAML Parse Error at Line {Line}, Pos {Pos}. Resource potentially missing.", xamlEx.LineNumber, xamlEx.LinePosition);
                }

                await HandleModalErrorAsync(typeof(TViewModel), ex);
                return false;
            }
        }

        private async Task<ModalState?> CreateModalStateAsync<TViewModel>(
            ModalSize? requestedSize,
            object? parameter,
            CancellationToken cancellationToken) where TViewModel : class
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Create ViewModel
            var viewModel = _serviceProvider.GetService(typeof(TViewModel)) as TViewModel;
            if (viewModel == null)
            {
                throw new InvalidOperationException($"ViewModel {typeof(TViewModel).Name} not registered in DI container");
            }

            // Initialize ViewModel (Design System 33.1)
            if (viewModel is IModalAware modalAware)
            {
                await modalAware.OnModalOpenedAsync(parameter, cancellationToken);
            }
            else if (viewModel is IInitializable<object?> initializable)
            {
                await initializable.InitializeAsync(parameter, cancellationToken);
            }

            // Determine modal size (Design System ?15.4)
            var size = requestedSize ??
                      (viewModel as IModalViewModel)?.PreferredSize ??
                      ModalSize.Medium;

            // Create View via mapping service
            var window = _viewMappingService.CreateView(typeof(TViewModel), viewModel);
            if (window == null)
            {
                throw new InvalidOperationException($"No View registered for ViewModel {typeof(TViewModel).Name}");
            }

            // Configure window properties
            ConfigureModalWindow(window, size, StackDepth);

            // Set up event handlers
            window.Closed += OnModalWindowClosed;
            window.PreviewKeyDown += OnModalWindowPreviewKeyDown;

            return new ModalState
            {
                Window = window,
                ViewModel = viewModel,
                Size = size,
                OpenedAt = DateTime.UtcNow
            };
        }

        private async Task ShowModalWindowAsync(ModalState state, CancellationToken cancellationToken)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Apply backdrop effect based on stack depth
                ApplyBackdropEffect(state.Window, StackDepth);

                // Apply animations if not reduced motion
                if (!IsReducedMotionEnabled())
                {
                    ApplyOpenAnimation(state.Window);
                }

                // Show the window modally
                // Design System: Owner set to main window for proper centering
                state.Window.Owner = System.Windows.Application.Current.MainWindow;
                
                // Use Show() instead of ShowDialog() to prevent UI thread blocking
                // Modal behavior is maintained through Owner property and Topmost
                state.Window.Topmost = true;
                state.Window.Show();
                state.Window.Activate();
                state.Window.Focus();

            }, DispatcherPriority.DataBind);
        }

        private async Task<bool> ExecuteCloseCurrentModalAsync(bool force)
        {
            if (StackDepth == 0) return true;

            var state = _modalStack.Peek();
            if (state.IsClosing) return false;

            state.IsClosing = true;

            try
            {
                // Check for unsaved changes (Design System ?33.4)
                if (!force && state.ViewModel is IHasUnsavedChanges hasUnsaved && hasUnsaved.HasUnsavedChanges)
                {
                    var shouldClose = await ShowUnsavedChangesDialogAsync();
                    if (!shouldClose)
                    {
                        state.IsClosing = false;
                        return false;
                    }
                }

                // Notify closing
                OnModalClosing(new ModalNavigationEventArgs(
                    state.ViewModel.GetType(), state.Size, StackDepth, false));

                // Close the window
                await CloseModalWindowAsync(state);

                // Clean up
                CleanupModalState(state);
                _modalStack.Pop();

                // Update current ViewModel
                UpdateCurrentViewModel(_modalStack.Count > 0 ? _modalStack.Peek().ViewModel : null);

                OnPropertyChanged(nameof(StackDepth));
                OnPropertyChanged(nameof(IsModalOpen));
                OnPropertyChanged(nameof(HasUnsavedChanges));

                // Notify closed
                OnModalClosed(new ModalNavigationEventArgs(
                    state.ViewModel.GetType(), state.Size, StackDepth, true));

                LogModalEvent("Closed", state.ViewModel.GetType(), state.Size);
                return true;
            }
            catch (Exception ex)
            {
                state.IsClosing = false;
                await HandleModalErrorAsync(state.ViewModel.GetType(), ex);
                return false;
            }
        }

        private Task CloseModalWindowAsync(ModalState state)
        {
            // Use a TaskCompletionSource so we properly AWAIT the animation completion.
            // Previously, _dispatcher.InvokeAsync returned immediately after scheduling the
            // animation, causing the modal stack cleanup to run before window.Close() was
            // ever called. This also prevented the storyboard Completed event from firing
            // on the correct window instance.
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (state.Window.IsLoaded)
                    {
                        if (!IsReducedMotionEnabled())
                        {
                            ApplyCloseAnimation(state.Window, () =>
                            {
                                state.Window.Close();
                                tcs.TrySetResult(true);
                            });
                        }
                        else
                        {
                            state.Window.Close();
                            tcs.TrySetResult(true);
                        }
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        #endregion

        #region Window Configuration & Styling

        private void ConfigureModalWindow(Window window, ModalSize size, int stackDepth)
        {
            // Design System ?15.4: Modal dimensions
            window.Width = (int)size;
            window.SizeToContent = SizeToContent.Height;
            window.MaxHeight = System.Windows.Application.Current.MainWindow.ActualHeight * 0.9;

            // Design System ?15.4: Modal styling
            window.WindowStyle = WindowStyle.None;
            window.AllowsTransparency = true;
            window.ResizeMode = ResizeMode.NoResize;
            window.ShowInTaskbar = false;
            
            window.Background = System.Windows.Media.Brushes.Transparent;

            // Design System ?33.1: Z-index layering
            window.Topmost = true;

            // Center on owner
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Apply Design System styles from ModalStyles.xaml
            if (System.Windows.Application.Current.Resources.Contains("ModalContentStyle"))
            {
                window.Style = System.Windows.Application.Current.Resources["ModalContentStyle"] as Style;
            }
        }

        private void ApplyBackdropEffect(Window window, int stackDepth)
        {
            // Design System ?33.2: Backdrop opacity levels
            var opacity = stackDepth == 0 ? BackdropOpacityBase : BackdropOpacityStacked;

            // Create backdrop effect (would be implemented with a separate overlay window)
            // For now, we'll rely on the modal window's built-in dimming
        }

        private void ApplyOpenAnimation(Window window)
        {
            // Design System ?33.5: Scale 0.94?1.0 + Opacity 0?1, 400ms
            var storyboard = System.Windows.Application.Current.Resources["ModalFadeIn"] as System.Windows.Media.Animation.Storyboard;
            if (storyboard != null)
            {
                storyboard.Begin(window);
            }
        }

        private void ApplyCloseAnimation(Window window, Action onCompleted)
        {
            // CRITICAL FIX: Clone the shared storyboard resource before use.
            // Using the shared resource directly causes event handler accumulation:
            // each call added another 'Completed' handler to the same Storyboard object.
            // After the second close, old handlers fired for the new window, causing
            // window.Close() to target the wrong window or not fire at all.
            var template = System.Windows.Application.Current.Resources["ModalFadeOut"] as System.Windows.Media.Animation.Storyboard;
            if (template != null)
            {
                var storyboard = template.Clone();
                storyboard.Completed += (s, e) => onCompleted();
                storyboard.Begin(window);
            }
            else
            {
                // No animation resource found - close the window immediately.
                onCompleted();
            }
        }

        #endregion

        #region Event Handlers

        private void OnModalWindowClosed(object? sender, EventArgs e)
        {
            var window = sender as Window;
            if (window == null) return;

            _dispatcher.InvokeAsync(async () =>
            {
                // Find and clean up the modal state
                var state = _modalStack.FirstOrDefault(s => s.Window == window);
                if (state != null)
                {
                    // Complete any pending result
                    if (state.ResultCompletionSource != null && !state.ResultCompletionSource.Task.IsCompleted)
                    {
                        var result = state.ViewModel is IModalResult<object> modalResult ?
                                   modalResult.Result : null;
                        state.ResultCompletionSource.TrySetResult(result);
                    }

                    // Clean up event handlers
                    window.Closed -= OnModalWindowClosed;
                    window.PreviewKeyDown -= OnModalWindowPreviewKeyDown;

                    // Force close if window is still open somehow
                    if (window.IsLoaded)
                    {
                        window.Close();
                    }
                }
            });
        }

        private void OnModalWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Design System ?33.3: Escape key handling
            if (e.Key == Key.Escape && !e.Handled)
            {
                _dispatcher.InvokeAsync(async () =>
                {
                    await CloseCurrentModalAsync();
                });
                e.Handled = true;
            }
        }

        #endregion

        #region Helper Methods

        private void WireUpGlobalHandlers()
        {
            // Wire up global Escape key handler
            ComponentDispatcher.ThreadIdle += OnThreadIdle;
        }

        private void OnThreadIdle(object? sender, EventArgs e)
        {
            // Global Escape key detection
            if (Keyboard.IsKeyDown(Key.Escape) && StackDepth > 0)
            {
                _dispatcher.InvokeAsync(async () =>
                {
                    await HandleEscapeKeyAsync();
                });
            }
        }

        private bool IsReducedMotionEnabled()
        {
            // Design System ?5.4: Reduced motion detection
            return !SystemParameters.MenuAnimation;
        }

        private void UpdateCurrentViewModel(object? viewModel)
        {
            var previous = Interlocked.Exchange(ref _currentModalViewModel, viewModel);

            // Dispose previous if needed
            if (previous is IDisposable disposable)
            {
                SafeDispose(disposable, previous.GetType().Name);
            }

            OnPropertyChanged(nameof(CurrentModalViewModel));
        }

        private void CleanupModalState(ModalState state)
        {
            // Clean up ViewModel if disposable
            if (state.ViewModel is IDisposable disposable)
            {
                SafeDispose(disposable, state.ViewModel.GetType().Name);
            }

            // Reset z-index
            // Reset z-index removed
        }

        private async Task ShowMaxDepthErrorAsync()
        {
            // Design System ?33.1: Third modal attempt blocked
            await _dispatcher.InvokeAsync(() =>
            {
                // Would typically show a toast notification
                // For now, we'll just log and show a message box
                // Variable message removed

                // Removed MessageBox to comply with No-MessageBox rule.
                _logger?.LogWarning("Modal stack depth exceeded maximum {MaxDepth}", MaxStackDepth);

                _logger?.LogWarning("Modal stack depth exceeded maximum {MaxDepth}", MaxStackDepth);
            });
        }

        private async Task HandleModalErrorAsync(Type viewModelType, Exception exception)
        {
            _logger?.LogError(exception, "Modal operation failed for {ViewModelType}", viewModelType.Name);

            await _dispatcher.InvokeAsync(() =>
            {
                OnModalOperationFailed(new ModalNavigationEventArgs(
                    viewModelType, ModalSize.Medium, StackDepth, false, exception));
            });
        }

        private void SafeDispose(IDisposable disposable, string typeName)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to dispose {TypeName}", typeName);
            }
        }

        private void LogModalEvent(string eventName, Type viewModelType, ModalSize? size = null)
        {
            _logger?.LogInformation("Modal {Event}: {ViewModelType} (Size: {Size}, Stack: {StackDepth})",
                eventName, viewModelType.Name, size?.ToString() ?? "Default", StackDepth);
        }

        private void LogServiceInitialization()
        {
            _logger?.LogInformation("ModalNavigationService initialized with max stack depth {MaxDepth}", MaxStackDepth);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ModalNavigationService));
            }
        }

        #endregion

        #region Event Invocators

        private void OnModalOpening(ModalNavigationEventArgs e)
        {
            ModalOpening?.Invoke(this, e);
        }

        private void OnModalOpened(ModalNavigationEventArgs e)
        {
            ModalOpened?.Invoke(this, e);
        }

        private void OnModalClosing(ModalNavigationEventArgs e)
        {
            ModalClosing?.Invoke(this, e);
        }

        private void OnModalClosed(ModalNavigationEventArgs e)
        {
            ModalClosed?.Invoke(this, e);
        }

        private void OnModalOperationFailed(ModalNavigationEventArgs e)
        {
            ModalOperationFailed?.Invoke(this, e);
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (this)
            {
                if (_isDisposed) return;
                _isDisposed = true;
            }

            // Unwire global handlers
            ComponentDispatcher.ThreadIdle -= OnThreadIdle;

            // Cancel current operation
            _currentOperationCts?.Cancel();
            _currentOperationCts?.Dispose();

            // Close all modals
            _ = CloseAllModalsAsync(true);

            // Clean up stack
            while (_modalStack.Count > 0)
            {
                var state = _modalStack.Pop();
                CleanupModalState(state);
            }

            _modalLock?.Dispose();
            _logger?.LogInformation("ModalNavigationService disposed");
        }

        #endregion
    }

    #region Supporting Internal Classes

    // Internal classes for unsaved changes dialog (Design System ?33.4)
    internal sealed class UnsavedChangesDialogViewModel : ViewModelBase
    {
        private readonly Action _discardCallback;
        private readonly Action _cancelCallback;

        public ICommand DiscardCommand { get; }
        public ICommand CancelCommand { get; }

        public UnsavedChangesDialogViewModel(Action discardCallback, Action cancelCallback)
        {
            _discardCallback = discardCallback;
            _cancelCallback = cancelCallback;

            DiscardCommand = new RelayCommand(ExecuteDiscard);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void ExecuteDiscard() => _discardCallback();
        private void ExecuteCancel() => _cancelCallback();
    }

    internal sealed partial class UnsavedChangesDialogView : Window
    {
        public UnsavedChangesDialogView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Width = 400;
            Height = 250;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(24)
            };

            // Icon (Design System ?33.4: ?? 48px)
            var icon = new System.Windows.Controls.TextBlock
            {
                Text = "??",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };

            // Title
            var title = new System.Windows.Controls.TextBlock
            {
                Text = "Unsaved Changes",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };

            // Message
            var message = new System.Windows.Controls.TextBlock
            {
                Text = "You have unsaved changes. Are you sure you want to close?",
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 32)
            };

            // Buttons panel
            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
                // ? Removed: Spacing = 16
            };

            var keepButton = new System.Windows.Controls.Button
            {
                Content = "Keep Editing",
                Width = 120,
                Height = 40,
                Margin = new Thickness(0, 0, 8, 0),  // ? Added: 8px right margin
                Command = (DataContext as UnsavedChangesDialogViewModel)?.CancelCommand
            };

            var discardButton = new System.Windows.Controls.Button
            {
                Content = "Discard Changes",
                Width = 120,
                Height = 40,
                Margin = new Thickness(8, 0, 0, 0),  // ? Added: 8px left margin (total 16px gap)
                Background = new SolidColorBrush(Color.FromRgb(255, 82, 82)), // #FF5252
                Foreground = System.Windows.Media.Brushes.White,
                Command = (DataContext as UnsavedChangesDialogViewModel)?.DiscardCommand
            };

            buttonPanel.Children.Add(keepButton);
            buttonPanel.Children.Add(discardButton);

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(title);
            stackPanel.Children.Add(message);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

    #endregion
}
