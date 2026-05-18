using System.Windows;
using System.Windows.Controls;

namespace Management.Presentation.Views.Shop
{
    public partial class AddProductView : UserControl
    {
        private ScrollViewer? _formScroll;
        private Controls.Premium.ScrollIndicatorButton? _formScrollIndicator;

        public AddProductView()
        {
            InitializeComponent();
        }

        private void FormScroll_Loaded(object sender, RoutedEventArgs e)
        {
            _formScroll = sender as ScrollViewer;
            TryWireForm();

            // Defer the visual tree walk to ensure the UserControl is fully attached to the ModalShellControl's visual tree
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new System.Action(() =>
            {
                DependencyObject parent = _formScroll;
                while (parent != null)
                {
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                    if (parent is ScrollViewer outerSv && outerSv != _formScroll)
                    {
                        outerSv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        break;
                    }
                }
            }));
        }

        private void FormScrollIndicator_Loaded(object sender, RoutedEventArgs e)
        {
            _formScrollIndicator = sender as Controls.Premium.ScrollIndicatorButton;
            TryWireForm();
        }

        private void TryWireForm()
        {
            if (_formScroll != null && _formScrollIndicator != null)
                _formScrollIndicator.TargetScrollViewer = _formScroll;
        }
    }
}
