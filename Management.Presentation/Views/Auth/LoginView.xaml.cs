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

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            // CRITICAL: Sync visible TextBox back to PasswordBox before Command execution
            // if the eye toggle (visible password) is currently active.
            if (PasswordTextBox.Visibility == Visibility.Visible)
            {
                PasswordInput.Password = PasswordTextBox.Text;
            }
        }


        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                // Switching to visible — sync from PasswordBox
                PasswordTextBox.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordTextBox.Focus();
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;

                // Swap icons
                if (EyeOpenIcon != null) EyeOpenIcon.Visibility = Visibility.Collapsed;
                if (EyeClosedIcon != null) EyeClosedIcon.Visibility = Visibility.Visible;
            }
            else
            {
                // Switching back to hidden — sync value first
                PasswordInput.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordInput.Focus();

                // Swap icons
                if (EyeOpenIcon != null) EyeOpenIcon.Visibility = Visibility.Visible;
                if (EyeClosedIcon != null) EyeClosedIcon.Visibility = Visibility.Collapsed;
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
