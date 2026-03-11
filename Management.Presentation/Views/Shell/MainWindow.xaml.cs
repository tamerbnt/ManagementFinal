using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.ComponentModel;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.Helpers;
using Management.Presentation.Resources.Controls;

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
