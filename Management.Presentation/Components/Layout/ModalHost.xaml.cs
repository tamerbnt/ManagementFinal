using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using Management.Presentation.Services.Infrastructure;
using Management.Presentation.Services;

namespace Management.Presentation.Components.Layout
{
    [ContentProperty("ModalContent")]
    public partial class ModalHost : UserControl
    {
        public static readonly DependencyProperty ModalContentProperty =
            DependencyProperty.Register("ModalContent", typeof(object), typeof(ModalHost), new PropertyMetadata(null));

        public object ModalContent
        {
            get => GetValue(ModalContentProperty);
            set => SetValue(ModalContentProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register("IsOpen", typeof(bool), typeof(ModalHost), new PropertyMetadata(false, OnIsOpenChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        private readonly IPerformanceService? _performanceService;

        public ModalHost()
        {
            InitializeComponent();
            if (System.Windows.Application.Current is App app && app.ServiceProvider != null)
            {
                _performanceService = app.ServiceProvider.GetService<IPerformanceService>();
                var modalService = app.ServiceProvider.GetRequiredService<IModalNavigationService>();
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (ModalHost)d;
            bool isOpen = (bool)e.NewValue;

            if (isOpen)
            {
                host.Visibility = Visibility.Visible;
                
                double blurRadius = 15;
                if (host._performanceService?.IsEcoMode == true)
                {
                    blurRadius = 0;
                    host.Backdrop.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E61E293B")); // 90% opacity
                }
                
                host.AnimateModal(1.0, 1.0, blurRadius);
            }
            else
            {
                host.AnimateModal(0.0, 0.9, 0, () => host.Visibility = Visibility.Collapsed);
            }
        }

        private void AnimateModal(double targetOpacity, double targetScale, double blurRadius, System.Action? onComplete = null)
        {
            var sb = new System.Windows.Media.Animation.Storyboard();
            var duration = System.TimeSpan.FromMilliseconds(300);
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

            // Backdrop Opacity
            var backdropAnim = new System.Windows.Media.Animation.DoubleAnimation { To = targetOpacity, Duration = duration, EasingFunction = ease };
            System.Windows.Media.Animation.Storyboard.SetTarget(backdropAnim, Backdrop);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(backdropAnim, new PropertyPath(Border.OpacityProperty));
            sb.Children.Add(backdropAnim);

            // Backdrop Blur
            var blurAnim = new System.Windows.Media.Animation.DoubleAnimation { To = blurRadius, Duration = duration, EasingFunction = ease };
            System.Windows.Media.Animation.Storyboard.SetTarget(blurAnim, BackdropBlur);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(blurAnim, new PropertyPath(System.Windows.Media.Effects.BlurEffect.RadiusProperty));
            sb.Children.Add(blurAnim);

            // Container Opacity
            var containerOpacity = new System.Windows.Media.Animation.DoubleAnimation { To = targetOpacity, Duration = duration, EasingFunction = ease };
            System.Windows.Media.Animation.Storyboard.SetTarget(containerOpacity, ModalContainer);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(containerOpacity, new PropertyPath(Border.OpacityProperty));
            sb.Children.Add(containerOpacity);

            // Container Scale
            var scaleX = new System.Windows.Media.Animation.DoubleAnimation { To = targetScale, Duration = duration, EasingFunction = ease };
            System.Windows.Media.Animation.Storyboard.SetTarget(scaleX, ModalScale);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));
            sb.Children.Add(scaleX);

            var scaleY = new System.Windows.Media.Animation.DoubleAnimation { To = targetScale, Duration = duration, EasingFunction = ease };
            System.Windows.Media.Animation.Storyboard.SetTarget(scaleY, ModalScale);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(scaleY);

            if (onComplete != null) sb.Completed += (s, e) => onComplete();
            sb.Begin();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            IsOpen = false;
        }

        private void Backdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsOpen = false;
        }
    }
}
