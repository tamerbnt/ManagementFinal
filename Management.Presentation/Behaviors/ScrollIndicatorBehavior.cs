using System;
using System.Windows;
using System.Windows.Controls;

namespace Management.Presentation.Behaviors
{
    /// <summary>
    /// Attached behavior that hooks into ScrollViewer events to track scroll position
    /// and determine when the user has scrolled up past a certain threshold.
    /// </summary>
    public static class ScrollIndicatorBehavior
    {
        /// <summary>
        /// Attached property representing the threshold distance from the bottom (in pixels)
        /// before the scroll indicator reveals itself.
        /// </summary>
        public static readonly DependencyProperty ShowIndicatorThresholdProperty =
            DependencyProperty.RegisterAttached(
                "ShowIndicatorThreshold",
                typeof(double),
                typeof(ScrollIndicatorBehavior),
                new PropertyMetadata(100.0));

        public static double GetShowIndicatorThreshold(DependencyObject obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return (double)obj.GetValue(ShowIndicatorThresholdProperty);
        }

        public static void SetShowIndicatorThreshold(DependencyObject obj, double value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.SetValue(ShowIndicatorThresholdProperty, value);
        }

        /// <summary>
        /// Attached property indicating whether the scroll viewer is currently scrolled
        /// up past the active threshold from the bottom.
        /// </summary>
        public static readonly DependencyProperty IsScrolledUpProperty =
            DependencyProperty.RegisterAttached(
                "IsScrolledUp",
                typeof(bool),
                typeof(ScrollIndicatorBehavior),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static bool GetIsScrolledUp(DependencyObject obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return (bool)obj.GetValue(IsScrolledUpProperty);
        }

        public static void SetIsScrolledUp(DependencyObject obj, bool value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.SetValue(IsScrolledUpProperty, value);
        }

        /// <summary>
        /// Attached property to register or unregister the high-performance scroll monitoring logic.
        /// </summary>
        public static readonly DependencyProperty RegisterScrollMonitoringProperty =
            DependencyProperty.RegisterAttached(
                "RegisterScrollMonitoring",
                typeof(bool),
                typeof(ScrollIndicatorBehavior),
                new PropertyMetadata(false, OnRegisterScrollMonitoringChanged));

        public static bool GetRegisterScrollMonitoring(DependencyObject obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return (bool)obj.GetValue(RegisterScrollMonitoringProperty);
        }

        public static void SetRegisterScrollMonitoring(DependencyObject obj, bool value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.SetValue(RegisterScrollMonitoringProperty, value);
        }

        private static void OnRegisterScrollMonitoringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                if ((bool)e.NewValue)
                {
                    scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                    // Trigger an immediate check on hook-up
                    EvaluateScrollState(scrollViewer);
                }
                else
                {
                    scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                }
            }
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                EvaluateScrollState(scrollViewer);
            }
        }

        private static void EvaluateScrollState(ScrollViewer scrollViewer)
        {
            double threshold = GetShowIndicatorThreshold(scrollViewer);
            
            // Calculate distance remaining to the absolute bottom
            double distanceToBottom = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;

            // Indicator should be active if content is scrollable and we are scrolled up above the threshold
            bool isScrolledUp = scrollViewer.ScrollableHeight > 0 && distanceToBottom > threshold;

            // Update state safely only if the value has mutated to prevent binding churn
            if (GetIsScrolledUp(scrollViewer) != isScrolledUp)
            {
                SetIsScrolledUp(scrollViewer, isScrolledUp);
            }
        }
    }
}
