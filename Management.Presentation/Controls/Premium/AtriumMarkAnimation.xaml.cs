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
        Scan,
        Spin
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

        public static readonly DependencyProperty IsScanningProperty = DependencyProperty.Register(
            nameof(IsScanning), typeof(bool), typeof(AtriumMarkAnimation),
            new PropertyMetadata(false, OnIsScanningChanged));

        public bool IsScanning
        {
            get => (bool)GetValue(IsScanningProperty);
            set => SetValue(IsScanningProperty, value);
        }

        private static void OnIsScanningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AtriumMarkAnimation control && (bool)e.NewValue)
            {
                control.AnimationMode = AtriumAnimationMode.Scan;
            }
            else if (d is AtriumMarkAnimation ctrl && !(bool)e.NewValue && ctrl.AnimationMode == AtriumAnimationMode.Scan)
            {
                ctrl.AnimationMode = AtriumAnimationMode.Static;
            }
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

        /// <summary>
        /// Forces the control to cycle through all animation states to prime the WPF storyboard engine.
        /// This prevents the first-time "hitch" when the animation is triggered by user interaction.
        /// </summary>
        public void WarmUp()
        {
            Dispatcher.InvokeAsync(async () => 
            {
                VisualStateManager.GoToElementState(RootLayout, "Construction", false);
                await System.Threading.Tasks.Task.Delay(50);
                VisualStateManager.GoToElementState(RootLayout, "Scan", false);
                await System.Threading.Tasks.Task.Delay(50);
                VisualStateManager.GoToElementState(RootLayout, "Pulse", false);
                await System.Threading.Tasks.Task.Delay(50);
                VisualStateManager.GoToElementState(RootLayout, "Spin", false);
                await System.Threading.Tasks.Task.Delay(50);
                UpdateAnimationState();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
            if (!IsVisible) return;

            if (ReduceMotion)
            {
                VisualStateManager.GoToElementState(RootLayout, "Static", true);
                return;
            }

            // PERFORMANCE FIX: Defer the VisualStateManager call using InvokeAsync with Render priority.
            // This separates the heavy storyboard initialization/compilation from the current layout pass,
            // preventing the UI thread stall reported during first-time use.
            Dispatcher.InvokeAsync(() => 
            {
                if (!IsVisible) return;

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
                    case AtriumAnimationMode.Spin:
                        VisualStateManager.GoToElementState(RootLayout, "Spin", true);
                        break;
                    case AtriumAnimationMode.Static:
                    default:
                        VisualStateManager.GoToElementState(RootLayout, "Static", true);
                        break;
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }
    }
}
