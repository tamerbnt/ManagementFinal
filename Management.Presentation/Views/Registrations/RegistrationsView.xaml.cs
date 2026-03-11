using System.Windows.Controls;
using Management.Presentation.ViewModels.Registrations;

namespace Management.Presentation.Views.Registrations
{
    public partial class RegistrationsView : UserControl
    {
        public RegistrationsView()
        {
            InitializeComponent();
        }

        public RegistrationsView(RegistrationsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
