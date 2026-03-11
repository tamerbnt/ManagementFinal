using System.Windows;
using Management.Presentation.ViewModels.Finance;

namespace Management.Presentation.Views.FinanceAndStaff
{
    public partial class PayrollView : Window
    {
        public PayrollView()
        {
            InitializeComponent();
        }

        public PayrollView(PayrollViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
