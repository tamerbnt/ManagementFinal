using System.Windows;
using System.Windows.Controls;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Auth
{
    public partial class LoginView : UserControl
    {
        private bool _isPasswordVisible = false;

        public LoginView()
        {
            InitializeComponent();
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                // Sync text to the TextBox and show it
                PasswordTextBox.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
                PasswordTextBox.Focus();

                // Swap icons
                EyeOpenIcon.Visibility = Visibility.Collapsed;
                EyeClosedIcon.Visibility = Visibility.Visible;
            }
            else
            {
                // Sync back to PasswordBox and show it
                PasswordInput.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordInput.Focus();

                // Swap icons
                EyeOpenIcon.Visibility = Visibility.Visible;
                EyeClosedIcon.Visibility = Visibility.Collapsed;
            }
        }

        // Kept for any future use / ViewModel sync fallback
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel && sender is PasswordBox passwordBox)
            {
                // Note: Password is passed via CommandParameter for security.
                // This hook is available if needed.
            }
        }
    }
}
