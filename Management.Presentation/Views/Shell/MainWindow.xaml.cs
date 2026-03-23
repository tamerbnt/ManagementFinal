using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += (s, e) => WindowHelper.EnableMica(this);
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

