using System.Windows.Controls;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Settings
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        // Default constructor for design-time support
        public SettingsView()
        {
            InitializeComponent();
        }

        // Constructor Injection for Dependency Injection
        public SettingsView(SettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}