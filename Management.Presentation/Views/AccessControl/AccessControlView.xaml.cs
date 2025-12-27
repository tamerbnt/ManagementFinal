using System.Windows.Controls;
using Management.Presentation.ViewModels;
using Management.Presentation.Views.AccessControl;

namespace Management.Presentation.Views.AccessControl
{
    /// <summary>
    /// Interaction logic for AccessControlView.xaml
    /// </summary>
    public partial class AccessControlView : UserControl
    {
        // Default constructor for design-time support
        public AccessControlView()
        {
            InitializeComponent();
        }

        // Dependency Injection Constructor
        // The DI container will inject the singleton/transient AccessControlViewModel here
        public AccessControlView(AccessControlViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}