using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Management.Presentation.Helpers;
using Management.Presentation.Behaviors;

namespace Management.Presentation.Controls.Premium
{
    /// <summary>
    /// Interaction logic for ScrollIndicatorButton.xaml.
    /// Acts as a floating, performance-optimized vector indicator to trigger
    /// animated smooth-scrolling to the bottom of a target ScrollViewer.
    /// </summary>
    public partial class ScrollIndicatorButton : UserControl
    {
        private ScrollViewer? _resolvedScrollViewer;

        /// <summary>
        /// DependencyProperty backing the target scroll viewer which this button controls.
        /// Can bind directly to a ScrollViewer or any FrameworkElement container like ListBox or ListView.
        /// </summary>
        public static readonly DependencyProperty TargetScrollViewerProperty =
            DependencyProperty.Register(
                nameof(TargetScrollViewer),
                typeof(FrameworkElement),
                typeof(ScrollIndicatorButton),
                new PropertyMetadata(null, OnTargetScrollViewerChanged));

        public FrameworkElement? TargetScrollViewer
        {
            get => (FrameworkElement?)GetValue(TargetScrollViewerProperty);
            set => SetValue(TargetScrollViewerProperty, value);
        }

        /// <summary>
        /// DependencyProperty that represents the active visibility state (whether the button should fade in).
        /// </summary>
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                nameof(IsActive),
                typeof(bool),
                typeof(ScrollIndicatorButton),
                new PropertyMetadata(false, OnActiveStateChanged));

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        /// <summary>
        /// DependencyProperty representing whether there is unread content at the bottom of the scroll view.
        /// </summary>
        public static readonly DependencyProperty HasUnreadProperty =
            DependencyProperty.Register(
                nameof(HasUnread),
                typeof(bool),
                typeof(ScrollIndicatorButton),
                new PropertyMetadata(false, OnHasUnreadChanged));

        public bool HasUnread
        {
            get => (bool)GetValue(HasUnreadProperty);
            set => SetValue(HasUnreadProperty, value);
        }

        public ScrollIndicatorButton()
        {
            InitializeComponent();
            this.Visibility = Visibility.Collapsed;
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (TargetScrollViewer == null)
            {
                var parent = this.Parent ?? System.Windows.Media.VisualTreeHelper.GetParent(this);
                if (parent != null)
                {
                    var target = FindSiblingTarget(parent);
                    if (target != null)
                    {
                        TargetScrollViewer = target;
                    }
                }
            }
        }

        private FrameworkElement? FindSiblingTarget(DependencyObject parent)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child == this) continue;

                if (child is ListBox || child is ListView || child is ScrollViewer || child is DataGrid)
                {
                    return child as FrameworkElement;
                }

                if (child is Panel panel)
                {
                    var found = FindSiblingTarget(panel);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void SetupScrollViewer(ScrollViewer scrollViewer)
        {
            CleanupScrollViewer();
            
            _resolvedScrollViewer = scrollViewer;
            _resolvedScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;

            // Trigger initial evaluation
            EvaluateScrollState();
        }

        private void CleanupScrollViewer()
        {
            if (_resolvedScrollViewer != null)
            {
                _resolvedScrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                _resolvedScrollViewer = null;
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            EvaluateScrollState();
        }

        private void EvaluateScrollState()
        {
            if (_resolvedScrollViewer == null) return;

            // If CanContentScroll is true, we are scrolling by ITEMS (logical scrolling). 10 items is too huge a threshold for small settings lists.
            // If CanContentScroll is false, we are scrolling by PIXELS (physical scrolling). 10 pixels is a good threshold.
            double threshold = _resolvedScrollViewer.CanContentScroll ? 1.0 : 10.0; 
            double distanceToBottom = _resolvedScrollViewer.ScrollableHeight - _resolvedScrollViewer.VerticalOffset;

            // Only show if the content is actually scrollable AND we are scrolled up above the threshold
            IsActive = _resolvedScrollViewer.ScrollableHeight > 0 && distanceToBottom > threshold;
        }

        private static void OnTargetScrollViewerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollIndicatorButton button)
            {
                button.CleanupScrollViewer();

                if (e.NewValue is ScrollViewer scrollViewer)
                {
                    button.SetupScrollViewer(scrollViewer);
                }
                else if (e.NewValue is FrameworkElement element)
                {
                    // Force template application to ensure visual tree exists
                    element.ApplyTemplate();

                    var sv = ScrollViewerHelper.FindVisualChild<ScrollViewer>(element);
                    if (sv != null)
                    {
                        button.SetupScrollViewer(sv);
                    }
                    else
                    {
                        // Fallback: search for ScrollViewer whenever the visual layout updates
                        EventHandler? layoutUpdatedHandler = null;
                        layoutUpdatedHandler = (s, ev) =>
                        {
                            var resolvedSv = ScrollViewerHelper.FindVisualChild<ScrollViewer>(element);
                            if (resolvedSv != null)
                            {
                                element.LayoutUpdated -= layoutUpdatedHandler;
                                button.SetupScrollViewer(resolvedSv);
                            }
                        };
                        element.LayoutUpdated += layoutUpdatedHandler;
                    }
                }
            }
        }

        private static void OnActiveStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollIndicatorButton button)
            {
                bool active = (bool)e.NewValue;
                var fadeIn = button.Resources["FadeInAnimation"] as Storyboard;
                var fadeOut = button.Resources["FadeOutAnimation"] as Storyboard;

                if (active)
                {
                    button.Visibility = Visibility.Visible;
                    fadeOut?.Stop(button);
                    fadeIn?.Begin(button);
                }
                else
                {
                    fadeIn?.Stop(button);
                    if (fadeOut != null)
                    {
                        // Clean up event handler to prevent leaks, then subscribe to close visual visibility on complete
                        EventHandler? completedHandler = null;
                        completedHandler = (s, ev) =>
                        {
                            fadeOut.Completed -= completedHandler;
                            // Ensure it's still inactive before collapsing
                            if (!button.IsActive)
                            {
                                button.Visibility = Visibility.Collapsed;
                            }
                        };

                        fadeOut.Completed += completedHandler;
                        fadeOut.Begin(button);
                    }
                    else
                    {
                        button.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private static void OnHasUnreadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollIndicatorButton button)
            {
                bool hasUnread = (bool)e.NewValue;
                button.UnreadBadge.Visibility = hasUnread ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ScrollBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_resolvedScrollViewer != null)
            {
                // Smooth scroll to the end
                _resolvedScrollViewer.SmoothScrollTo(_resolvedScrollViewer.ScrollableHeight);
                // Automatically dismiss the unread state on click
                HasUnread = false;
            }
        }

        private void ScrollBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Gently increase container opacity on hover
            var opacityAnim = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.15));
            ContainerBorder.BeginAnimation(Border.OpacityProperty, opacityAnim);

            // Play clean perpetual bounce on chevron
            var bounceAnim = new DoubleAnimation
            {
                From = 0,
                To = 2.5,
                Duration = TimeSpan.FromSeconds(0.25),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            IconTranslate.BeginAnimation(TranslateTransform.YProperty, bounceAnim);
        }

        private void ScrollBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Restore normal active state opacity
            var opacityAnim = new DoubleAnimation(0.9, TimeSpan.FromSeconds(0.15));
            ContainerBorder.BeginAnimation(Border.OpacityProperty, opacityAnim);

            // Stop hover bounce and return chevron to normal position
            IconTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        }

        private void ScrollBtn_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Tactile press shrink
            var shrinkX = new DoubleAnimation(0.92, TimeSpan.FromSeconds(0.05));
            var shrinkY = new DoubleAnimation(0.92, TimeSpan.FromSeconds(0.05));
            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkX);
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkY);
        }

        private void ScrollBtn_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Scale back
            var scaleX = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.05));
            var scaleY = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.05));
            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        }
    }
}
