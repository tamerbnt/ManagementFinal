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
using Management.Presentation.Controls.Premium;
using System.Linq;
using System.Windows.Media;

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

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // ── Constructor ──────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();

            // PERFORMANCE WARMING: Prime the brand animation engine for the main shell.
            WarmingAnim.WarmUp();

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

                // Fix 4 + Step 1.2: verify all Window.InputBindings resolved correctly.
                System.Diagnostics.Debug.WriteLine($"[KEYS-LOAD] Window Loaded");
                System.Diagnostics.Debug.WriteLine($"[KEYS-LOAD] DataContext={DataContext?.GetType().Name ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"[KEYS-LOAD] Window.InputBindings count={InputBindings.Count}");
                foreach (InputBinding ib in InputBindings)
                {
                    if (ib is KeyBinding kb)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[KEYS-LOAD]   KeyBinding: Key={kb.Key} Mod={kb.Modifiers} " +
                            $"Command={kb.Command?.GetType().Name ?? "NULL"} " +
                            $"CanExecute={kb.Command?.CanExecute(kb.CommandParameter)}");
                    }
                    Serilog.Log.Warning("[Shortcuts] InputBinding has NULL command — Key={Key}",
                        (ib as KeyBinding)?.Key);
                }

                // Also check RootGrid InputBindings (should be 0 after Fix 2)
                if (FindName("RootGrid") is FrameworkElement grid)
                {
                    System.Diagnostics.Debug.WriteLine($"[KEYS-LOAD] RootGrid.InputBindings count={grid.InputBindings.Count}");
                    foreach (InputBinding ib in grid.InputBindings)
                    {
                        if (ib is KeyBinding kb)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[KEYS-LOAD]   RootGrid KeyBinding: Key={kb.Key} Mod={kb.Modifiers} " +
                                $"Command={kb.Command?.GetType().Name ?? "NULL"} " +
                                $"CanExecute={kb.Command?.CanExecute(kb.CommandParameter)}");
                        }
                    }
                }

                Serilog.Log.Debug("[Shortcuts] Window loaded. InputBindings count={Count}", InputBindings.Count);
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
                    RECT workArea = monitorInfo.rcWork; // standard maximized space excluding taskbar

                    uint dpi = GetDpiForWindow(hwnd);
                    double scale = dpi > 0 ? dpi / 96.0 : 1.0;

                    // To respect Windows taskbar, bind exclusively to rcWork physical geometry.
                    // ptMaxPosition uses the work area physical offsets.
                    mmi.ptMaxPosition.X = workArea.Left;
                    mmi.ptMaxPosition.Y = workArea.Top;

                    // ptMaxSize uses the bounded box containing ONLY the visible usable area.
                    mmi.ptMaxSize.X     = workArea.Right  - workArea.Left;
                    mmi.ptMaxSize.Y     = workArea.Bottom - workArea.Top;


                    // Convert WPF logical MinWidth/MinHeight to physical pixels using
                    // the current monitor's DPI — so Windows enforces the minimum
                    // when the user drags the window border. MinTrackSize expects PHYSICAL pixels.
                    mmi.ptMinTrackSize.X = (int)(this.MinWidth * scale);
                    mmi.ptMinTrackSize.Y = (int)(this.MinHeight * scale);
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
            System.Diagnostics.Debug.WriteLine(
                $"[KEYS-TRACE] OnPreviewKeyDown: Key={e.Key} Modifiers={Keyboard.Modifiers} " +
                $"Handled={e.Handled} Source={e.Source?.GetType().Name} " +
                $"OriginalSource={e.OriginalSource?.GetType().Name} " +
                $"FocusedElement={Keyboard.FocusedElement?.GetType().Name}");

            base.OnPreviewKeyDown(e);

            System.Diagnostics.Debug.WriteLine(
                $"[KEYS-TRACE] After base.OnPreviewKeyDown: Handled={e.Handled}");

            if (e.Handled) return; // something in base already handled it

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

                    if (vm.IsSettingsOpen)
                    {
                        vm.CloseSettingsCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[KEYS-TRACE] OnKeyDown: Key={e.Key} Modifiers={Keyboard.Modifiers} Handled={e.Handled}");
            base.OnKeyDown(e);
            System.Diagnostics.Debug.WriteLine(
                $"[KEYS-TRACE] After base.OnKeyDown: Handled={e.Handled}");
        }

        protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            var focusedElement = Keyboard.FocusedElement as DependencyObject;
            if (focusedElement == null) return;

            // 1. Identify if the focused element is a search bar (or part of one)
            FrameworkElement? searchContainer = null;

            if (focusedElement is TextBox tb)
            {
                // Check if it's inside a PremiumSearchBox
                var premium = FindAncestor<PremiumSearchBox>(tb);
                if (premium != null)
                {
                    searchContainer = premium;
                }
                // Check if it's a named search box (e.g. GlobalSearchBox, LocalMemberSearchBox)
                else if (tb.Name != null && tb.Name.Contains("SearchBox", StringComparison.OrdinalIgnoreCase))
                {
                    searchContainer = tb;
                }
            }

            // 2. If a search bar is focused, check if the click is outside its bounds
            if (searchContainer != null)
            {
                var point = e.GetPosition(this);
                
                // Special Case: Global Search Box Popup
                // If it's the global search box, we must also check if the click is inside its results popup
                if (searchContainer is TextBox gtb && gtb.Name == "GlobalSearchBox")
                {
                    var topBar = FindAncestor<TopBarView>(gtb);
                    if (topBar != null && topBar.IsPointInsideSearch(point))
                    {
                        return; // Click is inside the search box or its popup results
                    }
                }
                else
                {
                    // For other search bars, just check the container bounds
                    var containerPoint = searchContainer.TranslatePoint(new Point(0, 0), this);
                    var containerRect = new Rect(containerPoint, new Size(searchContainer.ActualWidth, searchContainer.ActualHeight));
                    
                    if (containerRect.Contains(point))
                    {
                        return; // Click is inside the search bar
                    }
                }

                // 3. Click is outside -> Clear focus to trigger collapse/revert state
                this.Focus();
                Keyboard.ClearFocus();
            }
        }

        private bool _isExiting = false;

        /// <summary>
        /// Prepares the window for a shell handoff (e.g. logout), ensuring it closes
        /// silently without triggering the shutdown confirmation dialog or full app exit.
        /// </summary>
        public void PrepareForHandoff()
        {
            _isExiting = true;
        }

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

        private T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T target) return target;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }
    }
}

