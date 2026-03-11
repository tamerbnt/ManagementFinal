using System.Windows;
using System.Windows.Input;
using Management.Presentation.ViewModels;
using Management.Presentation.Services.Infrastructure;
using Management.Presentation.Services.Application;

namespace Management.Presentation.Views.Auth
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
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

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is LoginViewModel vm && sender is System.Windows.Controls.PasswordBox pb)
            {
                // vm.Password = pb.Password; // Password accessed via CommandParameter for security
            }
        }
    }
}
