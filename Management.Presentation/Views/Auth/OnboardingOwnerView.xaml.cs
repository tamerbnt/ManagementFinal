using System.Windows.Controls;

namespace Management.Presentation.Views.Auth
{
    /// <summary>
    /// Interaction logic for OnboardingOwnerView.xaml
    /// </summary>
    public partial class OnboardingOwnerView : UserControl
    {
        public OnboardingOwnerView()
        {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.OnboardingOwnerViewModel viewModel && sender is PasswordBox passwordBox)
            {
                viewModel.Password = passwordBox.Password;
            }
        }
    }
}
