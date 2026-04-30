using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Management.Presentation.Controls.Premium
{
    public partial class ThemeSegmentedToggle : UserControl
    {
        public static readonly DependencyProperty IsDarkThemeProperty =
            DependencyProperty.Register("IsDarkTheme", typeof(bool), typeof(ThemeSegmentedToggle), 
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDarkThemeChanged));

        public bool IsDarkTheme
        {
            get => (bool)GetValue(IsDarkThemeProperty);
            set => SetValue(IsDarkThemeProperty, value);
        }

        public ThemeSegmentedToggle()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdateVisualState(false);
        }

        private static void OnIsDarkThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ThemeSegmentedToggle control)
            {
                control.UpdateVisualState();
            }
        }

        private void UpdateVisualState(bool animate = true)
        {
            if (this.ActualWidth == 0) return;

            double panelWidth = this.ActualWidth - 4; // Border padding (2 on each side)
            double pillWidth = panelWidth / 2;
            
            // Only update width if it actually changed to avoid unnecessary layout passes
            if (double.IsNaN(SelectionPill.Width) || Math.Abs(SelectionPill.Width - pillWidth) > 0.1)
            {
                SelectionPill.Width = pillWidth;
            }

            double targetTranslate = IsDarkTheme ? pillWidth : 0;

            if (animate)
            {
                var anim = new DoubleAnimation(targetTranslate, TimeSpan.FromSeconds(0.2))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                PillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            else
            {
                PillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                PillTranslate.X = targetTranslate;
            }

            // Update icon colors based on theme
            var activeBrush = System.Windows.Application.Current.TryFindResource("TextPrimaryBrush") as Brush;
            var inactiveBrush = System.Windows.Application.Current.TryFindResource("TextTertiaryBrush") as Brush;

            SunIcon.Stroke = IsDarkTheme ? inactiveBrush : activeBrush;
            MoonIcon.Fill = IsDarkTheme ? activeBrush : inactiveBrush;
        }

        private void LightButton_Click(object sender, RoutedEventArgs e) => IsDarkTheme = false;
        private void DarkButton_Click(object sender, RoutedEventArgs e) => IsDarkTheme = true;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateVisualState(false);
        }
    }
}
