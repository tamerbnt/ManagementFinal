using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Management.Presentation.ViewModels.Shell;

namespace Management.Presentation.Components.Layout
{
    public partial class CommandPaletteView : UserControl
    {
        public CommandPaletteView()
        {
            InitializeComponent();
        }

        private void Backdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CommandPaletteViewModel vm)
            {
                vm.IsVisible = false;
            }
        }
    }
}
