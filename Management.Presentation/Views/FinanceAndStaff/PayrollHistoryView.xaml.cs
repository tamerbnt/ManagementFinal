using System.Windows;
using Management.Presentation.ViewModels.Finance;

namespace Management.Presentation.Views.FinanceAndStaff
{
    public partial class PayrollHistoryView : Window
    {
        public PayrollHistoryView()
        {
            InitializeComponent();
        }

        public PayrollHistoryView(PayrollHistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
