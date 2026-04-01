using System.Windows.Controls;
using Management.Presentation.ViewModels.Auth;

namespace Management.Presentation.Views.Auth
{
    public partial class SplashOnboardingView : UserControl
    {
        public SplashOnboardingView()
        {
            InitializeComponent();
        }

        public SplashOnboardingView(SplashOnboardingViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
