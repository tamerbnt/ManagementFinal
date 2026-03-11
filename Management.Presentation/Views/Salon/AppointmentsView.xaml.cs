using System.Windows.Controls;
using System.Windows;

namespace Management.Presentation.Views.Salon
{
    public partial class AppointmentsView : UserControl
    {
        public AppointmentsView()
        {
            InitializeComponent();
        }

        private void BodyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Sync Horizontal (Headers)
            if (e.HorizontalChange != 0 && HeaderScrollViewer != null)
            {
                HeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            }

            // Sync Vertical (Time Axis)
            if (e.VerticalChange != 0 && TimeAxisScrollViewer != null)
            {
                TimeAxisScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }
    }
}
