using System.Windows;
using Management.Presentation.ViewModels.Diagnostic;

namespace Management.Presentation.Views.Diagnostic
{
    /// <summary>
    /// Interaction logic for DiagnosticView.xaml
    /// </summary>
    public partial class DiagnosticView : Window
    {
        public DiagnosticView(DiagnosticViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
