using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.ComponentModel;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.Helpers;
using Management.Presentation.Resources.Controls;
using Management.Presentation.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.Views.Shell
{
    public partial class MainWindow : ModernWindow
    {
        private const double ExpandedWidth = 268;
        private const double CollapsedWidth = 72;

        // ── Win32 structs for WM_GETMINMAXINFO ──────────────────────────────
        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // ── Constructor ──────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += (s, e) =>
            {
                WindowHelper.EnableMica(this);
                // Hook WndProc to fix taskbar overlap when maximized
                var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source?.AddHook(WndProc);
            };
            this.Loaded += (s, e) =>
            {
                RootGrid.Focus();
                Keyboard.Focus(RootGrid);

                // Fix 4: verify all Window.InputBindings resolved correctly.
                // Any null Command means the BindingProxy did not receive the DataContext in time.
                Serilog.Log.Debug("[Shortcuts] Window loaded. InputBindings count={Count}", InputBindings.Count);
                foreach (InputBinding binding in InputBindings)
                {
                    if (binding.Command == null)
                    {
                        Serilog.Log.Warning("[Shortcuts] InputBinding has NULL command — Key={Key}",
                            (binding as KeyBinding)?.Key);
                    }
                }
            };
        }

        // Intercept WM_GETMINMAXINFO to constrain the maximized bounds to the
        // current monitor's work area (i.e. excluding the taskbar).
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    RECT workArea    = monitorInfo.rcWork;    // excludes taskbar
                    RECT monitorArea = monitorInfo.rcMonitor; // full screen

                    // ptMaxPosition is relative to the monitor's top-left corner
                    mmi.ptMaxPosition.X = workArea.Left   - monitorArea.Left;
                    mmi.ptMaxPosition.Y = workArea.Top    - monitorArea.Top;
                    mmi.ptMaxSize.X     = workArea.Right  - workArea.Left;
                    mmi.ptMaxSize.Y     = workArea.Bottom - workArea.Top;
                }

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public MainWindow(MainViewModel viewModel) : this()
        {
            DataContext = viewModel;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsModalOpen))
            {
                if (DataContext is MainViewModel vm && vm.IsModalOpen)
                {
                    // Focus the window first to ensure a clean focus state
                    this.Focus();
                    
                    // Delay slightly to allow the modal to be rendered and visible
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Transfer focus to the root layout to allow the modal's internal 
                        // FocusManager.FocusedElement to take effect.
                        Keyboard.Focus(this);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                // If a modal is open, close the topmost modal first
                if (DataContext is MainViewModel vm)
                {
                    var modalStore = vm.ModalStore;
                    if (modalStore != null && modalStore.IsOpen)
                    {
                        modalStore.Close();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            // Robust collapse: detect if we clicked outside the search bar
            // Find TopBarView in the visual tree
            var topBar = FindChild<TopBarView>(this);
            if (topBar != null)
            {
                var point = e.GetPosition(this);
                if (!topBar.IsPointInsideSearch(point))
                {
                    // Clicked outside search bar -> Clear focus to trigger collapse
                    if (topBar.SearchBox.IsFocused || topBar.SearchPopupElement.IsOpen)
                    {
                        // Transfer focus to the root layout or window
                        this.Focus();
                        System.Windows.Input.Keyboard.ClearFocus();
                    }
                }
            }
        }

        private bool _isExiting = false;

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isExiting)
            {
                base.OnClosing(e);
                return;
            }

            // Prevent immediate closing
            e.Cancel = true;
            _ = HandleClosingAsync();
        }

        private async Task HandleClosingAsync()
        {
            if (DataContext is MainViewModel vm)
            {
                // Show the modal and wait for result
                var result = await vm.RequestExitAsync();
                
                if (result != ExitModalResult.Cancel)
                {
                    // Allow shutdown
                    _isExiting = true;
                    System.Windows.Application.Current.Shutdown();
                }
            }
            else
            {
                // Fallback: if data context is not set, just shutdown
                _isExiting = true;
                System.Windows.Application.Current.Shutdown();
            }
        }

        private T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}

