using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Management.Presentation.Helpers
{
    /// <summary>
    /// Helper utilities for WPF ScrollViewer, providing custom attached properties
    /// and extension methods to enable high-performance smooth animated scrolling.
    /// </summary>
    public static class ScrollViewerHelper
    {
        /// <summary>
        /// Attached Dependency Property to enable smooth scrolling animation.
        /// Targets the read-only ScrollViewer.VerticalOffset property by proxy.
        /// </summary>
        public static readonly DependencyProperty AnimatedOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedOffset",
                typeof(double),
                typeof(ScrollViewerHelper),
                new FrameworkPropertyMetadata(0.0, OnAnimatedOffsetChanged));

        public static double GetAnimatedOffset(DependencyObject obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return (double)obj.GetValue(AnimatedOffsetProperty);
        }

        public static void SetAnimatedOffset(DependencyObject obj, double value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.SetValue(AnimatedOffsetProperty, value);
        }

        private static void OnAnimatedOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        /// <summary>
        /// Smoothly scrolls a ScrollViewer to the target vertical offset.
        /// </summary>
        /// <param name="scrollViewer">The scroll viewer to animate.</param>
        /// <param name="targetOffset">The target vertical scroll position.</param>
        /// <param name="durationMs">The duration of the scroll animation in milliseconds.</param>
        public static void SmoothScrollTo(this ScrollViewer scrollViewer, double targetOffset, double durationMs = 300)
        {
            if (scrollViewer == null) return;

            // Clear any active animation to prevent conflicts
            scrollViewer.BeginAnimation(AnimatedOffsetProperty, null);

            // Seed the starting value of our property with the current offset
            SetAnimatedOffset(scrollViewer, scrollViewer.VerticalOffset);

            // Animate using a cubic ease out function (natural physics deceleration)
            var animation = new DoubleAnimation
            {
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            scrollViewer.BeginAnimation(AnimatedOffsetProperty, animation);
        }

        /// <summary>
        /// Finds the first visual child of a specific type recursively in the visual tree.
        /// </summary>
        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
        }
    }
}
