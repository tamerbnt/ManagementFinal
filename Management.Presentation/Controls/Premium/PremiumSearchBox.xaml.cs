using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Management.Presentation.Controls.Premium
{
    public partial class PremiumSearchBox : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(PremiumSearchBox), 
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register("Placeholder", typeof(string), typeof(PremiumSearchBox), 
            new PropertyMetadata(string.Empty));

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty IconDataProperty =
            DependencyProperty.Register("IconData", typeof(Geometry), typeof(PremiumSearchBox), 
            new PropertyMetadata(null));

        public Geometry IconData
        {
            get => (Geometry)GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public PremiumSearchBox()
        {
            InitializeComponent();
        }

        private void ClearText_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            Text = string.Empty;
            e.Handled = true;
        }

        private void Container_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            PART_TextBox.Focus();
        }
    }
}
