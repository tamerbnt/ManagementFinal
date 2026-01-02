using System.Windows;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views
{
    public partial class DiagnosticWindow : Window
    {
        public DiagnosticWindow(DiagnosticViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            Closed += (s, e) => viewModel.Cleanup();
        }
    }
}
