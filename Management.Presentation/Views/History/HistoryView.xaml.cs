using System.Windows.Controls;
using Management.Presentation.ViewModels.History;

namespace Management.Presentation.Views.History
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml
    /// </summary>
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();
        }

        // Constructor Injection for Dependency Injection
        public HistoryView(HistoryViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
