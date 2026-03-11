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
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            
            // Default chrome settings if not overridden
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 32,
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0), // Can be overridden
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });
        }
    }
}
