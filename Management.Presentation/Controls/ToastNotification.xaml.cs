using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Management.Presentation.Controls
{
    public partial class ToastNotification : UserControl
    {
        public ToastNotification()
        {
            InitializeComponent();
        }

        public enum ToastType
        {
            Success,
            Error,
            Warning
        }

        public void Show(ToastType type, string title, string message)
        {
            // Set content
            TitleText.Text = title;
            MessageText.Text = message;

            // Configure based on type
            switch (type)
            {
                case ToastType.Success:
                    ConfigureSuccess();
                    break;
                case ToastType.Error:
                    ConfigureError();
                    break;
                case ToastType.Warning:
                    ConfigureWarning();
                    break;
            }
        }

        private void ConfigureSuccess()
        {
            // Border & Shadow
            ToastBorder.BorderBrush = CreateGradientBrush("#6610B981", "#3310B981", "#6610B981");
            ToastShadow.Color = (Color)ColorConverter.ConvertFromString("#10B981");

            // Show success icon
            SuccessIcon.Visibility = Visibility.Visible;
            ErrorIcon.Visibility = Visibility.Collapsed;
            WarningIcon.Visibility = Visibility.Collapsed;

            // Animate icon (scale in with bounce)
            var scaleAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.4),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            SuccessScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            SuccessScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void ConfigureError()
        {
            // Border & Shadow
            ToastBorder.BorderBrush = CreateGradientBrush("#66EF4444", "#33EF4444", "#66EF4444");
            ToastShadow.Color = (Color)ColorConverter.ConvertFromString("#EF4444");

            // Show error icon
            SuccessIcon.Visibility = Visibility.Collapsed;
            ErrorIcon.Visibility = Visibility.Visible;
            WarningIcon.Visibility = Visibility.Collapsed;

            // Animate icon (shake)
            var shakeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(0.4)
            };
            shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2))));
            shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3))));
            shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))));
            ErrorShake.BeginAnimation(TranslateTransform.XProperty, shakeAnim);
        }

        private void ConfigureWarning()
        {
            // Border & Shadow
            ToastBorder.BorderBrush = CreateGradientBrush("#66F59E0B", "#33F59E0B", "#66F59E0B");
            ToastShadow.Color = (Color)ColorConverter.ConvertFromString("#F59E0B");

            // Show warning icon
            SuccessIcon.Visibility = Visibility.Collapsed;
            ErrorIcon.Visibility = Visibility.Collapsed;
            WarningIcon.Visibility = Visibility.Visible;

            // Animate icon (pulse)
            var pulseAnim = new DoubleAnimation
            {
                From = 0.6,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.8),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2)
            };
            WarningIcon.BeginAnimation(OpacityProperty, pulseAnim);
        }

        private LinearGradientBrush CreateGradientBrush(string color1, string color2, string color3)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color1), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color2), 0.5));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color3), 1));
            return brush;
        }

        public void Dismiss()
        {
            // Exit animation
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };

            var slideOut = new DoubleAnimation
            {
                To = 100,
                Duration = TimeSpan.FromSeconds(0.2)
            };

            fadeOut.Completed += (s, e) =>
            {
                // Remove from parent
                if (Parent is Panel panel)
                {
                    panel.Children.Remove(this);
                }
            };

            BeginAnimation(OpacityProperty, fadeOut);
            SlideTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }
    }
}
