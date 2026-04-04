using System.Windows;
using Management.Presentation.ViewModels.History;

namespace Management.Presentation.Views.Dashboard
{
    /// <summary>
    /// Interaction logic for InventoryHistoryView.xaml
    /// </summary>
    public partial class InventoryHistoryView : Window
    {
        public InventoryHistoryView()
        {
            InitializeComponent();
        }

        public InventoryHistoryView(InventoryHistoryViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
