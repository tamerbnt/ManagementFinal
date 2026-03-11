using System.Windows;
using System.Windows.Input;
using Management.Presentation.ViewModels.Onboarding;
using Management.Presentation.ViewModels;
using Management.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.Views.Auth
{
    public partial class AuthWindow : Window
    {
        public AuthWindow()
        {
            InitializeComponent();
            
            // Initialize toast notification service
            var app = System.Windows.Application.Current as App;
            var toastService = app?.ServiceProvider?.GetService<IToastNotificationService>() as ToastNotificationService;
            toastService?.Initialize(ToastContainer);
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
