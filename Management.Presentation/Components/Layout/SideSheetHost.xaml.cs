using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Management.Presentation.Services.Infrastructure;
using Management.Presentation.Services;

namespace Management.Presentation.Components.Layout
{
    public partial class SideSheetHost : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(SideSheetHost), new PropertyMetadata("Details"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register("IsOpen", typeof(bool), typeof(SideSheetHost), new PropertyMetadata(false, OnIsOpenChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        private readonly IPerformanceService? _performanceService;

        public SideSheetHost()
        {
            InitializeComponent();
            if (System.Windows.Application.Current is App app && app.ServiceProvider != null)
            {
                // var sideSheetService = app.ServiceProvider.GetRequiredService<ISideSheetService>();
                _performanceService = app.ServiceProvider.GetService<IPerformanceService>();
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (SideSheetHost)d;
            bool isOpen = (bool)e.NewValue;

            if (isOpen)
            {
                host.Visibility = Visibility.Visible;
                
                if (host._performanceService?.IsEcoMode == true)
                {
                    // For SideSheet, we can also simplify the backdrop or remove shadow if needed
                    // But primarily we ensure it responds to the performance tier
                }
                
                host.AnimateSheet(0);
            }
            else
            {
                host.AnimateSheet(450, () => host.Visibility = Visibility.Collapsed);
            }
        }

        private void AnimateSheet(double targetX, System.Action? onComplete = null)
        {
            var storyboard = new System.Windows.Media.Animation.Storyboard();
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = targetX,
                Duration = System.TimeSpan.FromMilliseconds(400),
                EasingFunction = new System.Windows.Media.Animation.ExponentialEase { Exponent = 6, EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            System.Windows.Media.Animation.Storyboard.SetTarget(animation, SheetTransform);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(animation, new PropertyPath(TranslateTransform.XProperty));
            storyboard.Children.Add(animation);

            if (onComplete != null)
            {
                storyboard.Completed += (s, e) => onComplete();
            }

            storyboard.Begin();
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
