using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace Management.Presentation.Behaviors
{
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
            EnsureTransform();
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
            EnsureTransform();

            // Initial State Sync
            var transform = (TranslateTransform)AssociatedObject.RenderTransform;
            double targetX = IsOpen ? 0 : AssociatedObject.ActualWidth;

            // Snap immediately on load
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = targetX;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // If closed and resizing (e.g. window resize), keep hidden off-screen
            if (!IsOpen)
            {
                EnsureTransform();
                var transform = (TranslateTransform)AssociatedObject.RenderTransform;
                transform.X = AssociatedObject.ActualWidth;
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (GlobalSidePanelBehavior)d;
            behavior.AnimatePanel((bool)e.NewValue);
        }

        // --- Logic ---

        private void EnsureTransform()
        {
            if (AssociatedObject == null) return;

            // Check if Transform exists, is the wrong type, or is Frozen (Immutable)
            if (AssociatedObject.RenderTransform == null ||
                !(AssociatedObject.RenderTransform is TranslateTransform) ||
                AssociatedObject.RenderTransform.IsFrozen)
            {
                // Replace with a new mutable TranslateTransform
                AssociatedObject.RenderTransform = new TranslateTransform();
            }
        }

        private void AnimatePanel(bool isOpen)
        {
            if (AssociatedObject == null) return;

            // 1. Safety check
            EnsureTransform();
            var transform = (TranslateTransform)AssociatedObject.RenderTransform;
            double targetX = isOpen ? 0 : AssociatedObject.ActualWidth;

            // 2. Initial Load Safety (Prevents Crash)
            // If not loaded, or Reduced Motion is on, snap instantly
            if (!AssociatedObject.IsLoaded || !SystemParameters.MenuAnimation)
            {
                transform.BeginAnimation(TranslateTransform.XProperty, null); // Stop any running anims
                transform.X = targetX;
                return;
            }

            // 3. Configure Physics
            IEasingFunction easing = isOpen
                ? new CubicEase { EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseIn };

            // 4. Animate
            var animation = new DoubleAnimation
            {
                To = targetX,
                Duration = Duration,
                EasingFunction = easing
            };

            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }
}
