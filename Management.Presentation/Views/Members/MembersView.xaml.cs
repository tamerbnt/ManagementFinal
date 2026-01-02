using System.Windows.Controls;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Members
{
    /// <summary>
    /// Interaction logic for MembersView.xaml
    /// </summary>
    public partial class MembersView : UserControl
    {
        public MembersView()
        {
            InitializeComponent();
        }

        // Constructor Injection for Dependency Injection
        public MembersView(MembersViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}