using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors; // Requires NuGet: Microsoft.Xaml.Behaviors.Wpf

namespace Management.Presentation.Behaviors
{
    /// <summary>
    /// Animates a FrameworkElement sliding in/out from the right edge.
    /// Compliant with Design System Section 5.3 (Motion Physics) and 5.4 (Reduced Motion).
    /// </summary>
    public class GlobalSidePanelBehavior : Behavior<FrameworkElement>
    {
        // --- Dependency Properties ---

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(GlobalSidePanelBehavior),
                new PropertyMetadata(false, OnIsOpenChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(Duration), typeof(GlobalSidePanelBehavior),
                new PropertyMetadata(new Duration(TimeSpan.FromSeconds(0.3)))); // DurationMedium

        public Duration Duration
        {
            get => (Duration)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        // --- Lifecycle ---

        protected override void OnAttached()
        {
            base.OnAttached();

            // Prepare the transform
            if (!(AssociatedObject.RenderTransform is TranslateTransform))
            {
                AssociatedObject.RenderTransform = new TranslateTransform();
            }

            // Hook into lifecycle to handle initial positioning
            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.SizeChanged += OnSizeChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.SizeChanged -= OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initial State Check:
            // If not open, force it off-screen immediately so it doesn't flicker visible
            if (!IsOpen)
            {
                var transform = (TranslateTransform)AssociatedObject.RenderTransform;
                transform.X = AssociatedObject.ActualWidth;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // If the panel resizes while closed (e.g. window resize), 
            // ensure it stays hidden off-screen by updating the X offset.
            if (!IsOpen)
            {
                var transform = (TranslateTransform)AssociatedObject.RenderTransform;
                transform.X = AssociatedObject.ActualWidth;
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (GlobalSidePanelBehavior)d;
            if (behavior.AssociatedObject == null) return;

            behavior.AnimatePanel((bool)e.NewValue);
        }

        // --- Animation Engine ---

        private void AnimatePanel(bool isOpen)
        {
            var transform = (TranslateTransform)AssociatedObject.RenderTransform;
            double targetX = isOpen ? 0 : AssociatedObject.ActualWidth;

            // 1. Accessibility Check (Section 5.4)
            if (!SystemParameters.MenuAnimation)
            {
                // Snap instantly
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                transform.X = targetX;
                return;
            }

            // 2. Configure Physics (Section 5.3)
            // Open: Decelerate (EaseOut) for entering
            // Close: Accelerate (EaseIn) for exiting
            IEasingFunction easing = isOpen
                ? new CubicEase { EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseIn };

            // 3. Create Animation
            var animation = new DoubleAnimation
            {
                To = targetX,
                Duration = Duration,
                EasingFunction = easing
            };

            // 4. Execute
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }
}