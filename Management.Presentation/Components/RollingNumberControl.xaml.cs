using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Management.Presentation.Components
{
    public partial class RollingNumberControl : UserControl
    {
        public static readonly DependencyProperty TargetValueProperty =
            DependencyProperty.Register(
                nameof(TargetValue),
                typeof(double),
                typeof(RollingNumberControl),
                new PropertyMetadata(0.0, OnTargetValueChanged));

        private double _currentValue = 0.0;

        public double TargetValue
        {
            get => (double)GetValue(TargetValueProperty);
            set => SetValue(TargetValueProperty, value);
        }

        public RollingNumberControl()
        {
            InitializeComponent();
        }

        private static void OnTargetValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RollingNumberControl control)
            {
                control.AnimateToValue((double)e.NewValue);
            }
        }

        private void AnimateToValue(double newValue)
        {
            var storyboard = new Storyboard();

            // Create animation for the number value
            var numberAnimation = new DoubleAnimation
            {
                From = _currentValue,
                To = newValue,
                Duration = TimeSpan.FromSeconds(1.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Update text during animation
            numberAnimation.CurrentTimeInvalidated += (s, e) =>
            {
                if (s is AnimationClock clock && clock.CurrentProgress.HasValue)
                {
                    var currentAnimatedValue = _currentValue + (newValue - _currentValue) * clock.CurrentProgress.Value;
                    NumberText.Text = Math.Round(currentAnimatedValue, 0).ToString("N0");
                }
            };

            numberAnimation.Completed += (s, e) =>
            {
                _currentValue = newValue;
                NumberText.Text = Math.Round(newValue, 0).ToString("N0");
            };

            storyboard.Children.Add(numberAnimation);
            Storyboard.SetTarget(numberAnimation, this);
            Storyboard.SetTargetProperty(numberAnimation, new PropertyPath("(FrameworkElement.Tag)"));

            storyboard.Begin();
        }
    }
}
