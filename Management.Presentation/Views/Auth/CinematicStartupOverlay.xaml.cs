using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Management.Presentation.Views.Auth
{
    public partial class CinematicStartupOverlay : UserControl
    {
        public static readonly DependencyProperty IsAnimationActiveProperty =
            DependencyProperty.Register("IsAnimationActive", typeof(bool), typeof(CinematicStartupOverlay), new PropertyMetadata(true));

        public bool IsAnimationActive
        {
            get { return (bool)GetValue(IsAnimationActiveProperty); }
            set { SetValue(IsAnimationActiveProperty, value); }
        }

        private Storyboard? _storyboard;

        public CinematicStartupOverlay()
        {
            InitializeComponent();
            
            // Register the DynamicBrush in the Namescope so the Storyboard can find it.
            // Items in Resources are not automatically added to the control's namescope.
            if (Resources["DynamicBrush"] is SolidColorBrush brush)
            {
                this.RegisterName("DynamicBrush", brush);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (IsAnimationActive)
            {
                _storyboard = (Storyboard)Resources["CinematicStartupStoryboard"];
                _storyboard.Begin(this, true); // true = controllable (allows SkipToFill)
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
            }
        }

        private void OnSkipAnimation(object sender, InputEventArgs e)
        {
            if (IsAnimationActive && _storyboard != null)
            {
                _storyboard.SkipToFill(this);
            }
        }

        private void OnAnimationCompleted(object sender, EventArgs e)
        {
            IsAnimationActive = false;
            this.Visibility = Visibility.Collapsed;
        }
    }
}
