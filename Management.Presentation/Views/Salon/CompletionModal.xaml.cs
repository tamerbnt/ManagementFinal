using System.Windows;

namespace Management.Presentation.Views.Salon
{
    public partial class CompletionModal : Window
    {
        public CompletionModal()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CompletionViewModel vm)
            {
                vm.CancelCommand.Execute(null);
            }
            else
            {
                this.Close();
            }
        }
    }
}
