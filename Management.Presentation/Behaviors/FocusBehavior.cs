using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Management.Presentation.Behaviors
{
    public static class FocusBehavior
    {
        public static readonly DependencyProperty FocusOnVisibleProperty =
            DependencyProperty.RegisterAttached(
                "FocusOnVisible",
                typeof(bool),
                typeof(FocusBehavior),
                new PropertyMetadata(false, OnFocusOnVisibleChanged));

        public static bool GetFocusOnVisible(DependencyObject obj)
        {
            return (bool)obj.GetValue(FocusOnVisibleProperty);
        }

        public static void SetFocusOnVisible(DependencyObject obj, bool value)
        {
            obj.SetValue(FocusOnVisibleProperty, value);
        }

        private static void OnFocusOnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.IsVisibleChanged += Element_IsVisibleChanged;
                    element.Loaded += Element_Loaded;
                }
                else
                {
                    element.IsVisibleChanged -= Element_IsVisibleChanged;
                    element.Loaded -= Element_Loaded;
                }
            }
        }

        private static void Element_Loaded(object sender, RoutedEventArgs e)
        {
            TryFocus(sender as FrameworkElement);
        }

        private static void Element_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                TryFocus(sender as FrameworkElement);
            }
        }

        private static void TryFocus(FrameworkElement element)
        {
            if (element == null || !element.IsVisible) return;

            // Use Dispatcher to ensure visual tree is ready
            element.Dispatcher.InvokeAsync(() =>
            {
                element.Focus();
                if (element is TextBoxBase textBox)
                {
                    textBox.SelectAll();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
