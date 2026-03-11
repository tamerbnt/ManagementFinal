using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace Management.Presentation.Behaviors
{
    public class ListItemEntranceBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty FlashOverlayNameProperty =
            DependencyProperty.Register(nameof(FlashOverlayName), typeof(string), typeof(ListItemEntranceBehavior), new PropertyMetadata(null));

        public string FlashOverlayName
        {
            get => (string)GetValue(FlashOverlayNameProperty);
            set => SetValue(FlashOverlayNameProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(Duration), typeof(ListItemEntranceBehavior), new PropertyMetadata(default(Duration)));

        public Duration Duration
        {
            get => (Duration)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            // Stub: Animation logic could go here
        }
    }
}
