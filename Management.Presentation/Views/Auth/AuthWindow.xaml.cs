using System.Windows;
using System.Windows.Input;
using Management.Presentation.ViewModels.Onboarding;
using Management.Presentation.ViewModels;
using Management.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Management.Presentation.Views.Auth
{
    public partial class AuthWindow : Window
    {
        public AuthWindow()
        {
            InitializeComponent();
            
            DataContextChanged += AuthWindow_DataContextChanged;
        }

        private void AuthWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is INotifyPropertyChanged newVm)
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri("../../Resources/Converters.xaml", System.UriKind.Relative) });
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri("../../Resources/Branding.Gym.xaml", System.UriKind.Relative) });
                
                newVm.PropertyChanged += ViewModel_PropertyChanged;
                UpdateCardProportions();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentView")
            {
                UpdateCardProportions();
            }
        }

        private void UpdateCardProportions()
        {
            if (DataContext == null) return;

            var propInfo = DataContext.GetType().GetProperty("CurrentView");
            if (propInfo != null)
            {
                var currentView = propInfo.GetValue(DataContext);
                
                if (currentView is ViewModels.Auth.SplashOnboardingViewModel)
                {
                    // Ensure full screen for splash
                    CardBorder.Width = 980;
                    CardBorder.Margin = new Thickness(0);
                    CardBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                else if (currentView is LoginViewModel)
                {
                    // Animate transition to Login card
                    AnimateToLogin();
                }
                else if (currentView is LicenseEntryViewModel)
                {
                    CardBorder.VerticalAlignment = VerticalAlignment.Center;
                    CardBorder.Width = 450;
                    CardBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    CardBorder.Margin = new Thickness(50, 50, 0, 50);
                }
                else
                {
                    CardBorder.VerticalAlignment = VerticalAlignment.Stretch;
                    CardBorder.Width = 450;
                    CardBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    CardBorder.Margin = new Thickness(50, 50, 0, 50);
                }
            }
        }

        private void AnimateToLogin()
        {
            if (CardBorder.Width == 450) return; // Already there

            var duration = new Duration(System.TimeSpan.FromSeconds(0.8));
            var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

            var widthAnim = new System.Windows.Media.Animation.DoubleAnimation(450, duration) { EasingFunction = easing };
            var marginAnim = new System.Windows.Media.Animation.ThicknessAnimation(new Thickness(50, 50, 0, 50), duration) { EasingFunction = easing };
            
            // Lock alignment to Left before animating width down from full
            CardBorder.HorizontalAlignment = HorizontalAlignment.Left;
            
            CardBorder.BeginAnimation(WidthProperty, widthAnim);
            CardBorder.BeginAnimation(MarginProperty, marginAnim);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}
