using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors; // Requires NuGet: Microsoft.Xaml.Behaviors.Wpf

namespace Management.Presentation.Behaviors
{
    /// <summary>
    /// Triggers a Slide-In + Fade-In + Flash Highlight animation when the element loads.
    /// Designed for high-frequency virtualized lists (e.g., Access Logs).
    /// </summary>
    public class ListItemEntranceBehavior : Behavior<FrameworkElement>
    {
        // --- Dependency Properties ---

        public static readonly DependencyProperty FlashOverlayNameProperty =
            DependencyProperty.Register(nameof(FlashOverlayName), typeof(string), typeof(ListItemEntranceBehavior), new PropertyMetadata(null));

        public string FlashOverlayName
        {
            get => (string)GetValue(FlashOverlayNameProperty);
            set => SetValue(FlashOverlayNameProperty, value);
        }

        public static readonly DependencyProperty SlideDistanceProperty =
            DependencyProperty.Register(nameof(SlideDistance), typeof(double), typeof(ListItemEntranceBehavior), new PropertyMetadata(-20.0));

        public double SlideDistance
        {
            get => (double)GetValue(SlideDistanceProperty);
            set => SetValue(SlideDistanceProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(Duration), typeof(ListItemEntranceBehavior),
                new PropertyMetadata(new Duration(TimeSpan.FromSeconds(0.3))));

        public Duration Duration
        {
            get => (Duration)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        // --- Lifecycle ---

        protected override void OnAttached()
        {
            base.OnAttached();

            // Ensure we have a TransformGroup or TranslateTransform to animate
            EnsureTransform();

            AssociatedObject.Loaded += OnLoaded;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.Loaded -= OnLoaded;
        }

        private void EnsureTransform()
        {
            if (!(AssociatedObject.RenderTransform is TranslateTransform))
            {
                AssociatedObject.RenderTransform = new TranslateTransform();
                AssociatedObject.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 1. Accessibility Check (Section 5.4)
            if (!SystemParameters.MenuAnimation)
            {
                ResetVisualState(instant: true);
                return;
            }

            // 2. Perform Animation
            RunEntranceAnimation();
        }

        private void ResetVisualState(bool instant)
        {
            if (instant)
            {
                AssociatedObject.Opacity = 1;
                if (AssociatedObject.RenderTransform is TranslateTransform tt)
                {
                    tt.Y = 0;
                }

                var flash = GetFlashOverlay();
                if (flash != null) flash.Opacity = 0;
            }
        }

        private void RunEntranceAnimation()
        {
            var transform = AssociatedObject.RenderTransform as TranslateTransform;
            if (transform == null) return;

            // --- A. Slide In (TranslateY) ---
            var slideAnim = new DoubleAnimation
            {
                From = SlideDistance,
                To = 0,
                Duration = Duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // --- B. Fade In (Opacity) ---
            // Start slightly invisible
            AssociatedObject.Opacity = 0;
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = Duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // --- C. Flash Highlight (Overlay) ---
            var flashOverlay = GetFlashOverlay();
            if (flashOverlay != null)
            {
                var flashAnim = new DoubleAnimation
                {
                    From = 0.3, // Start visible
                    To = 0,     // Fade to transparent
                    Duration = new Duration(TimeSpan.FromSeconds(0.8)), // Slower fade out
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                flashOverlay.BeginAnimation(UIElement.OpacityProperty, flashAnim);
            }

            // Execute Main Animations
            transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            AssociatedObject.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private UIElement? GetFlashOverlay()
        {
            if (string.IsNullOrEmpty(FlashOverlayName)) return null;
            return AssociatedObject.FindName(FlashOverlayName) as UIElement;
        }
    }
}