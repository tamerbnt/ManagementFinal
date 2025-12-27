using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors; // Requires NuGet: Microsoft.Xaml.Behaviors.Wpf

namespace Management.Presentation.Behaviors
{
    /// <summary>
    /// Applies a "Zoom In + Fade In" animation when the attached element opens.
    /// Compliant with Design System Section 5.3 (Modal Animations) and 5.4 (Reduced Motion).
    /// </summary>
    public class ModalEntranceBehavior : Behavior<FrameworkElement>
    {
        // --- Dependency Properties ---

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ModalEntranceBehavior),
                new PropertyMetadata(false, OnIsOpenChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(Duration), typeof(ModalEntranceBehavior),
                new PropertyMetadata(new Duration(TimeSpan.FromSeconds(0.4))));

        public Duration Duration
        {
            get => (Duration)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        // --- Lifecycle ---

        protected override void OnAttached()
        {
            base.OnAttached();

            // Ensure the pivot point is center for the zoom effect
            AssociatedObject.RenderTransformOrigin = new Point(0.5, 0.5);

            // Initialize the transform if not already present
            if (!(AssociatedObject.RenderTransform is ScaleTransform))
            {
                AssociatedObject.RenderTransform = new ScaleTransform(1, 1);
            }

            // If starting open, ensure visual state is correct
            if (IsOpen)
            {
                AssociatedObject.Opacity = 1;
                ((ScaleTransform)AssociatedObject.RenderTransform).ScaleX = 1;
                ((ScaleTransform)AssociatedObject.RenderTransform).ScaleY = 1;
            }
            else
            {
                // Default hidden state
                AssociatedObject.Opacity = 0;
                AssociatedObject.Visibility = Visibility.Collapsed;
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (ModalEntranceBehavior)d;
            if (behavior.AssociatedObject == null) return;

            bool isOpen = (bool)e.NewValue;
            if (isOpen)
            {
                behavior.AnimateIn();
            }
            else
            {
                behavior.AnimateOut();
            }
        }

        // --- Animation Logic ---

        private void AnimateIn()
        {
            var target = AssociatedObject;
            var scaleTransform = target.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            target.RenderTransform = scaleTransform;

            target.Visibility = Visibility.Visible;

            // 1. Accessibility Check (Section 5.4)
            if (!SystemParameters.MenuAnimation)
            {
                target.Opacity = 1;
                scaleTransform.ScaleX = 1;
                scaleTransform.ScaleY = 1;
                return;
            }

            // 2. Prepare Initial State (0.94 Scale, 0 Opacity)
            scaleTransform.ScaleX = 0.94;
            scaleTransform.ScaleY = 0.94;
            target.Opacity = 0;

            // 3. Configure Easing (Apple 2025 Standard)
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 4. Create Animations
            var fadeAnim = new DoubleAnimation
            {
                To = 1,
                Duration = Duration,
                EasingFunction = easing
            };

            var scaleAnim = new DoubleAnimation
            {
                To = 1,
                Duration = Duration,
                EasingFunction = easing
            };

            // 5. Execute
            target.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void AnimateOut()
        {
            var target = AssociatedObject;

            // Accessibility Check
            if (!SystemParameters.MenuAnimation)
            {
                target.Visibility = Visibility.Collapsed;
                target.Opacity = 0;
                return;
            }

            // Simple Fade Out (Faster than entrance)
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)), // Fast Exit
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                // Verify we are still "closed" before collapsing (handling rapid toggles)
                if (!IsOpen)
                {
                    target.Visibility = Visibility.Collapsed;
                }
            };

            target.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}