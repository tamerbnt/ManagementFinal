using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Management.Presentation.Views.Shell
{
    public partial class SidebarView : UserControl
    {
        public bool IsCollapsed
        {
            get { return (bool)GetValue(IsCollapsedProperty); }
            set { SetValue(IsCollapsedProperty, value); }
        }

        public static readonly DependencyProperty IsCollapsedProperty =
            DependencyProperty.Register("IsCollapsed", typeof(bool), typeof(SidebarView), new PropertyMetadata(false));

        public SidebarView()
        {
            InitializeComponent();
        }

        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            IsCollapsed = !IsCollapsed;
            var sb = (Storyboard)FindResource(IsCollapsed ? "CollapseSidebar" : "ExpandSidebar");
            sb.Begin();
        }
    }
}
