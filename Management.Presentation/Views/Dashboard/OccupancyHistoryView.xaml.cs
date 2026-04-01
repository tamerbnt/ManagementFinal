using Management.Presentation.ViewModels.Dashboard;
using System.Windows;

namespace Management.Presentation.Views.Dashboard
{
    public partial class OccupancyHistoryView : Window
    {
        public OccupancyHistoryView(OccupancyHistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
