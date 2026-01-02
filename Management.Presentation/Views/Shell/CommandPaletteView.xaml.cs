using System.Windows.Controls;
using System.Windows.Input;
using Management.Presentation.Services;

namespace Management.Presentation.Views.Shell
{
    public partial class CommandPaletteView : UserControl
    {
        public CommandPaletteView()
        {
            InitializeComponent();
            this.Loaded += (s, e) => SearchBox.Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (DataContext is ICommandPaletteService vm)
            {
                if (e.Key == Key.Escape) vm.Close();
                else if (e.Key == Key.Enter) vm.ExecuteSelected();
                else if (e.Key == Key.Down || e.Key == Key.Up)
                {
                    // Basic arrow navigation logic
                    int currentIndex = vm.Results.IndexOf(vm.Results.FirstOrDefault(r => r.IsSelected)!);
                    if (currentIndex != -1)
                    {
                        vm.Results[currentIndex].IsSelected = false;
                        int nextIndex = e.Key == Key.Down 
                            ? (currentIndex + 1) % vm.Results.Count 
                            : (currentIndex - 1 + vm.Results.Count) % vm.Results.Count;
                        vm.Results[nextIndex].IsSelected = true;
                    }
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }
    }
}
