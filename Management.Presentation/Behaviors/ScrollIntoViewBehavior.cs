using System.Windows;
using System.Windows.Controls;

namespace Management.Presentation.Behaviors
{
    public static class ScrollIntoViewBehavior
    {
        public static readonly DependencyProperty AutoScrollToSelectionProperty =
            DependencyProperty.RegisterAttached(
                "AutoScrollToSelection",
                typeof(bool),
                typeof(ScrollIntoViewBehavior),
                new PropertyMetadata(false, OnAutoScrollToSelectionChanged));

        public static bool GetAutoScrollToSelection(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToSelectionProperty);
        }

        public static void SetAutoScrollToSelection(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToSelectionProperty, value);
        }

        private static void OnAutoScrollToSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                if ((bool)e.NewValue)
                {
                    listBox.SelectionChanged += ListBox_SelectionChanged;
                }
                else
                {
                    listBox.SelectionChanged -= ListBox_SelectionChanged;
                }
            }
        }

        private static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                listBox.ScrollIntoView(listBox.SelectedItem);
            }
        }
    }
}
