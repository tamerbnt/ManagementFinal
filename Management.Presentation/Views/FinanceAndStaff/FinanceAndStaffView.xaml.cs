using System.Windows.Controls;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.FinanceAndStaff
{
    /// <summary>
    /// Interaction logic for FinanceAndStaffView.xaml
    /// </summary>
    public partial class FinanceAndStaffView : UserControl
    {
        // Default constructor required for XAML designer
        public FinanceAndStaffView()
        {
            InitializeComponent();
        }

        // Constructor Injection for Dependency Injection (Runtime)
        public FinanceAndStaffView(FinanceAndStaffViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}