using System.Windows.Controls;
using Management.Presentation.ViewModels.Shop;

namespace Management.Presentation.Views.Shop
{
    /// <summary>
    /// Interaction logic for ShopView.xaml
    /// </summary>
    public partial class ShopView : UserControl
    {
        // Default constructor for design-time support
        public ShopView()
        {
            InitializeComponent();
        }

        // Constructor Injection for Dependency Injection
        public ShopView(ShopViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
