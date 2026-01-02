using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace Management.Presentation.Behaviors
{
    public class FocusTrapBehavior : Behavior<Window>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.KeyDown += OnKeyDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.KeyDown -= OnKeyDown;
            base.OnDetaching();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                var direction = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                    ? FocusNavigationDirection.Previous
                    : FocusNavigationDirection.Next;

                var focusedElement = Keyboard.FocusedElement as UIElement;
                if (focusedElement != null)
                {
                    var nextElement = focusedElement.PredictFocus(direction);
                    if (nextElement == null)
                    {
                        // Wrap around
                        var first = GetFirstTabElement(AssociatedObject);
                        var last = GetLastTabElement(AssociatedObject);
                        
                        if (direction == FocusNavigationDirection.Next)
                            first?.Focus();
                        else
                            last?.Focus();
                        
                        e.Handled = true;
                    }
                }
            }
        }

        private UIElement? GetFirstTabElement(DependencyObject root)
        {
            return GetFocusableElement(AssociatedObject, true);
        }

        private UIElement? GetLastTabElement(DependencyObject root)
        {
            return GetFocusableElement(AssociatedObject, false);
        }

        private UIElement? GetFocusableElement(DependencyObject root, bool first)
        {
            var elements = new List<UIElement>();
            FindFocusableElements(root, elements);
 
            if (elements.Count == 0) return null;
            return first ? elements[0] : elements[elements.Count - 1];
        }

        private void FindFocusableElements(DependencyObject root, List<UIElement> elements)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is UIElement ui && ui.Focusable && ui.Visibility == Visibility.Visible)
                {
                    elements.Add(ui);
                }
                FindFocusableElements(child, elements);
            }
        }
    }
}
