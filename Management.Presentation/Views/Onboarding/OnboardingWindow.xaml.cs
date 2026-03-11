using System.Windows;
using System.Windows.Input;
using Management.Presentation.ViewModels.Onboarding;

namespace Management.Presentation.Views.Onboarding
{
    public partial class OnboardingWindow : Window
    {
        public OnboardingWindow(OnboardingViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}
