using System.Windows.Controls;
using Management.Presentation.ViewModels.Shop;

namespace Management.Presentation.Views.Shop
{
    /// <summary>
    /// Interaction logic for LogRestockView.xaml
    /// </summary>
    public partial class LogRestockView : UserControl
    {
        public LogRestockView()
        {
            InitializeComponent();
        }

        public LogRestockView(LogRestockViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
