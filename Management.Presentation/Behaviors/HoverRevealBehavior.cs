using System.Windows;
// Using Microsoft.Xaml.Behaviors package
using Microsoft.Xaml.Behaviors;

namespace Management.Presentation.Behaviors
{
    public class HoverRevealBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty TargetNameProperty =
            DependencyProperty.Register(nameof(TargetName), typeof(string), typeof(HoverRevealBehavior), new PropertyMetadata(null));

        public string TargetName
        {
            get => (string)GetValue(TargetNameProperty);
            set => SetValue(TargetNameProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(double), typeof(HoverRevealBehavior), new PropertyMetadata(0.2));

        public double Duration
        {
            get => (double)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.MouseEnter += (s, e) => AssociatedObject.Opacity = 1.0;
            AssociatedObject.MouseLeave += (s, e) => AssociatedObject.Opacity = 0.7;
        }
    }
}
