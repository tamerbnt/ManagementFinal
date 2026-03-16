using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;

namespace Management.Presentation.Views.Shell
{
    public partial class TopBarView : UserControl
    {
        public TopBarView()
        {
            InitializeComponent();
            
            WeakReferenceMessenger.Default.Register<FocusSearchMessage>(this, (r, m) => 
            {
                GlobalSearchBox.Focus();
                Keyboard.Focus(GlobalSearchBox);
            });
        }

        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Window.GetWindow(this)?.DragMove();
            }
        }

        private void OnMinimize(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).WindowState = WindowState.Minimized;
        }

        private void OnMaximize(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window.WindowState == WindowState.Maximized)
                window.WindowState = WindowState.Normal;
            else
                window.WindowState = WindowState.Maximized;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        public bool IsPointInsideSearch(Point pointInWindow)
        {
            try
            {
                var window = Window.GetWindow(this);
                if (window == null) return false;

                // 1. Check TextBox
                var textBoxPoint = this.GlobalSearchBox.TranslatePoint(new Point(0, 0), window);
                var textBoxRect = new Rect(textBoxPoint, new Size(GlobalSearchBox.ActualWidth, GlobalSearchBox.ActualHeight));
                if (textBoxRect.Contains(pointInWindow)) return true;

                // 2. Check Popup content (if open)
                if (SearchPopup.IsOpen && SearchPopup.Child is FrameworkElement popupContent)
                {
                    var popupPoint = popupContent.TranslatePoint(new Point(0, 0), window);
                    var popupRect = new Rect(popupPoint, new Size(popupContent.ActualWidth, popupContent.ActualHeight));
                    if (popupRect.Contains(pointInWindow)) return true;
                }
            }
            catch { }
            
            return false; 
        }

        internal TextBox SearchBox => GlobalSearchBox;
        internal Popup SearchPopupElement => SearchPopup;

        private void OnSearchTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.KeyDown += (s, args) => 
                {
                    if (args.Key == Key.Escape)
                    {
                        // Remove focus to trigger collapse
                        Keyboard.ClearFocus();
                        DependencyObject parent = VisualTreeHelper.GetParent(textBox);
                        while (parent != null && !(parent is Window)) parent = VisualTreeHelper.GetParent(parent);
                        (parent as Window)?.Focus();
                    }
                };
            }
        }

        private void OnSearchItemClicked(object sender, RoutedEventArgs e)
        {
            // Remove keyboard focus from the search box to trigger its collapse animation
            Keyboard.ClearFocus();
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null && !(parent is Window)) parent = VisualTreeHelper.GetParent(parent);
            (parent as Window)?.Focus();
        }

        private void OnProfileMenuItemClicked(object sender, RoutedEventArgs e)
        {
            // Explicitly uncheck the profile toggle to hide the popup immediately upon selection
            if (ProfileToggle != null)
            {
                ProfileToggle.IsChecked = false;
            }
        }
    }
}
