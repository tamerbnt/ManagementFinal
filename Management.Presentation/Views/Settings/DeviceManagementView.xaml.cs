using System.Windows.Controls;
using Management.Presentation.ViewModels.Settings;

namespace Management.Presentation.Views.Settings
{
    public partial class DeviceManagementView : UserControl
    {
        public DeviceManagementView()
        {
            InitializeComponent();
            this.Loaded += async (s, e) => 
            {
                if (DataContext is DeviceManagementViewModel viewModel)
                {
                    await viewModel.LoadDevicesAsync();
                }
            };
        }
    }
}
