using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Management.Domain.Enums;

namespace Management.Presentation.Controls.Premium
{
    public partial class SegmentedToggleControl : UserControl
    {
        public static readonly DependencyProperty SelectedGenderProperty =
            DependencyProperty.Register("SelectedGender", typeof(Gender), typeof(SegmentedToggleControl), 
            new FrameworkPropertyMetadata(Gender.Male, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedGenderChanged));

        public Gender SelectedGender
        {
            get => (Gender)GetValue(SelectedGenderProperty);
            set => SetValue(SelectedGenderProperty, value);
        }

        public SegmentedToggleControl()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdateVisualState(false);
        }

        private static void OnSelectedGenderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SegmentedToggleControl control)
            {
                control.UpdateVisualState();
            }
        }

        private void UpdateVisualState(bool animate = true)
        {
            if (this.ActualWidth == 0) return;

            double panelWidth = this.ActualWidth - 8; // Border padding
            double pillWidth = panelWidth / 2;
            
            // Only update width if it actually changed to avoid unnecessary layout passes
            if (Math.Abs(SelectionPill.Width - pillWidth) > 0.1)
            {
                SelectionPill.Width = pillWidth;
            }

            double targetTranslate = SelectedGender == Gender.Male ? 0 : pillWidth;

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
        }

        private void MaleButton_Click(object sender, RoutedEventArgs e) => SelectedGender = Gender.Male;
        private void FemaleButton_Click(object sender, RoutedEventArgs e) => SelectedGender = Gender.Female;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateVisualState(false);
        }
    }
}
