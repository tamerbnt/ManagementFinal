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
            if (e.NewValue is INotifyPropertyChanged newVm)
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri("../../Resources/Converters.xaml", System.UriKind.Relative) });
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri("../../Resources/Branding.Gym.xaml", System.UriKind.Relative) });
            }
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
