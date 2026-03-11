using System.Windows;
using System.Windows.Controls;

namespace Management.Presentation.Views.Auth
{
    public partial class OnboardingOwnerView : UserControl
    {
        private bool _isPasswordVisible = false;

        public OnboardingOwnerView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Syncs the PasswordBox value to the ViewModel when the user types (hidden mode).
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.OnboardingOwnerViewModel viewModel && sender is PasswordBox passwordBox)
            {
                viewModel.Password = passwordBox.Password;
            }
        }

        /// <summary>
        /// Syncs the TextBox value to the ViewModel when the user types (visible mode).
        /// </summary>
        private void OnboardingPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is ViewModels.OnboardingOwnerViewModel viewModel && sender is TextBox textBox)
            {
                viewModel.Password = textBox.Text;
                // Keep PasswordBox in sync so CommandParameter fallbacks still work
                OnboardingPasswordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                OnboardingPasswordBox.Password = textBox.Text;
                OnboardingPasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        /// <summary>
        /// Toggles password visibility between PasswordBox (hidden) and TextBox (plain text).
        /// </summary>
        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                // Transfer content to TextBox and show it
                OnboardingPasswordTextBox.TextChanged -= OnboardingPasswordTextBox_TextChanged;
                OnboardingPasswordTextBox.Text = OnboardingPasswordBox.Password;
                OnboardingPasswordTextBox.TextChanged += OnboardingPasswordTextBox_TextChanged;

                OnboardingPasswordBox.Visibility = Visibility.Collapsed;
                OnboardingPasswordTextBox.Visibility = Visibility.Visible;
                OnboardingPasswordTextBox.CaretIndex = OnboardingPasswordTextBox.Text.Length;
                OnboardingPasswordTextBox.Focus();

                OnboardingEyeOpenIcon.Visibility = Visibility.Collapsed;
                OnboardingEyeClosedIcon.Visibility = Visibility.Visible;
            }
            else
            {
                // Transfer back to PasswordBox and hide TextBox
                OnboardingPasswordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                OnboardingPasswordBox.Password = OnboardingPasswordTextBox.Text;
                OnboardingPasswordBox.PasswordChanged += PasswordBox_PasswordChanged;

                OnboardingPasswordTextBox.Visibility = Visibility.Collapsed;
                OnboardingPasswordBox.Visibility = Visibility.Visible;
                OnboardingPasswordBox.Focus();

                OnboardingEyeOpenIcon.Visibility = Visibility.Visible;
                OnboardingEyeClosedIcon.Visibility = Visibility.Collapsed;
            }
        }
    }
}
