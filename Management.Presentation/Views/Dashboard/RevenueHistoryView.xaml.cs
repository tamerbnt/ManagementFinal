using Management.Presentation.ViewModels.Dashboard;
using System.Windows;

namespace Management.Presentation.Views.Dashboard
{
    public partial class RevenueHistoryView : Window
    {
        public RevenueHistoryView(RevenueHistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
