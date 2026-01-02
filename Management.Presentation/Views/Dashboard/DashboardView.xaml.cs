using System.Windows.Controls;
using Management.Presentation.ViewModels;
// Ensure this namespace matches the location of your ViewModel
using Management.Presentation.Views.Dashboard;

namespace Management.Presentation.Views.Dashboard
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : UserControl
    {
        // Default constructor for XAML design-time support (optional but recommended)
        public DashboardView()
        {
            InitializeComponent();
        }

        // Dependency Injection Constructor
        // The DI container will automatically pass the DashboardViewModel here
        public DashboardView(DashboardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}