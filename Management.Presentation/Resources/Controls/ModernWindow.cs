using System.Windows;
using System.Windows.Shell;

namespace Management.Presentation.Resources.Controls
{
    public class ModernWindow : Window
    {
        public ModernWindow()
        {
            // Default styles for modern windows
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = false;
            this.Background = System.Windows.Media.Brushes.White; // Solid background for HW acceleration
        }
    }
}
