using System;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace Management.Presentation.Behaviors
{
    /// <summary>
    /// Animates the Opacity of a named target element based on MouseEnter/MouseLeave.
    /// COMPLIANCE: Respects Design System Section 5.4 (Reduced Motion).
    /// </summary>
    public class HoverRevealBehavior : Behavior<FrameworkElement>
    {
        // --- Dependency Properties ---

        public static readonly DependencyProperty TargetNameProperty =
            DependencyProperty.Register(nameof(TargetName), typeof(string), typeof(HoverRevealBehavior), new PropertyMetadata(null));

        public string TargetName
        {
            get => (string)GetValue(TargetNameProperty);
            set => SetValue(TargetNameProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(Duration), typeof(HoverRevealBehavior), new PropertyMetadata(new Duration(TimeSpan.FromSeconds(0.22))));

        public Duration Duration
        {
            get => (Duration)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        // --- Lifecycle ---

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.MouseEnter += OnMouseEnter;
            AssociatedObject.MouseLeave += OnMouseLeave;
            AssociatedObject.Loaded += OnLoaded;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.MouseEnter -= OnMouseEnter;
            AssociatedObject.MouseLeave -= OnMouseLeave;
            AssociatedObject.Loaded -= OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initial State: Hidden
            var target = GetTargetElement();
            if (target != null)
            {
                target.Opacity = 0;
                target.IsHitTestVisible = false;
            }
        }

        // --- Interaction Logic ---

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateTo(1.0, true);
        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateTo(0.0, false);
        }

        // --- Animation Engine ---

        private void AnimateTo(double opacity, bool isHitTestVisible)
        {
            var target = GetTargetElement();
            if (target == null) return;

            // 1. Update Input State
            target.IsHitTestVisible = isHitTestVisible;

            // 2. Accessibility Check (Section 5.4)
            // If user has requested Reduced Motion, skip animation
            if (!SystemParameters.MenuAnimation)
            {
                target.BeginAnimation(UIElement.OpacityProperty, null); // Stop existing
                target.Opacity = opacity;
                return;
            }

            // 3. Configure Animation
            var animation = new DoubleAnimation
            {
                To = opacity,
                Duration = Duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 4. Execute
            target.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private UIElement GetTargetElement()
        {
            if (string.IsNullOrEmpty(TargetName) || AssociatedObject == null)
                return null;

            return AssociatedObject.FindName(TargetName) as UIElement;
        }
    }
}