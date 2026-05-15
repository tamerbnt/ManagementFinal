using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Management.Presentation.Controls.Premium
{
    public enum AtriumAnimationMode
    {
        Static,
        Construction,
        Pulse,
        Scan
    }

    public partial class AtriumMarkAnimation : UserControl
    {
        public AtriumMarkAnimation()
        {
            InitializeComponent();
            // FIX: Use IsVisibleChanged instead of Loaded.
            // In WPF, Loaded fires even when Visibility="Collapsed", causing the
            // storyboard to play invisibly. IsVisibleChanged fires only when the
            // control transitions to Visible, which is exactly when we want the
            // animation to begin.
            IsVisibleChanged += OnVisibilityChanged;
        }

        public static readonly DependencyProperty AnimationModeProperty = DependencyProperty.Register(
            nameof(AnimationMode), typeof(AtriumAnimationMode), typeof(AtriumMarkAnimation),
            new PropertyMetadata(AtriumAnimationMode.Static, OnAnimationModeChanged));

        public AtriumAnimationMode AnimationMode
        {
            get => (AtriumAnimationMode)GetValue(AnimationModeProperty);
            set => SetValue(AnimationModeProperty, value);
        }

        public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.Register(
            nameof(ReduceMotion), typeof(bool), typeof(AtriumMarkAnimation),
            new PropertyMetadata(false, OnAnimationModeChanged));

        public bool ReduceMotion
        {
            get => (bool)GetValue(ReduceMotionProperty);
            set => SetValue(ReduceMotionProperty, value);
        }

        public static readonly DependencyProperty MarkForegroundProperty = DependencyProperty.Register(
            nameof(MarkForeground), typeof(Brush), typeof(AtriumMarkAnimation),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8C3D28"))));

        public Brush MarkForeground
        {
            get => (Brush)GetValue(MarkForegroundProperty);
            set => SetValue(MarkForegroundProperty, value);
        }

        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Only start the animation when transitioning TO visible.
            if (e.NewValue is bool isVisible && isVisible)
            {
                UpdateAnimationState();
            }
        }

        private static void OnAnimationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Guard against firing when the control is not yet in the visual tree.
            if (d is AtriumMarkAnimation control && control.IsVisible)
            {
                control.UpdateAnimationState();
            }
        }

        private void UpdateAnimationState()
        {
            if (ReduceMotion)
            {
                VisualStateManager.GoToElementState(RootLayout, "Static", true);
                return;
            }

            switch (AnimationMode)
            {
                case AtriumAnimationMode.Construction:
                    VisualStateManager.GoToElementState(RootLayout, "Construction", true);
                    break;
                case AtriumAnimationMode.Pulse:
                    VisualStateManager.GoToElementState(RootLayout, "Pulse", true);
                    break;
                case AtriumAnimationMode.Scan:
                    VisualStateManager.GoToElementState(RootLayout, "Scan", true);
                    break;
                case AtriumAnimationMode.Static:
                default:
                    VisualStateManager.GoToElementState(RootLayout, "Static", true);
                    break;
            }
        }
    }
}
