using System.Windows;
using System.Windows.Controls;

namespace Management.Presentation.Views.Salon
{
    public partial class AppointmentDetailModal : System.Windows.Window
    {
        public AppointmentDetailModal()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppointmentDetailViewModel vm)
            {
                vm.CloseCommand.Execute(null);
            }
            else
            {
                this.Close();
            }
        }
    }
}
