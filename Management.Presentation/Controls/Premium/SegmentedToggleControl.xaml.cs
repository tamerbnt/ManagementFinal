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
        }

        private static void OnSelectedGenderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SegmentedToggleControl control)
            {
                control.UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            double targetX = SelectedGender == Gender.Male ? 0 : this.ActualWidth / 2 - 4; // Subtraction for padding
            
            // Wait for measure if necessary
            if (this.ActualWidth == 0)
            {
                this.Loaded += (s, e) => UpdateVisualState();
                return;
            }

            double panelWidth = this.ActualWidth - 8; // Border padding
            double pillWidth = panelWidth / 2;
            SelectionPill.Width = pillWidth;

            double targetTranslate = SelectedGender == Gender.Male ? 0 : pillWidth;

            var anim = new DoubleAnimation(targetTranslate, TimeSpan.FromSeconds(0.25))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            
            PillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void MaleButton_Click(object sender, RoutedEventArgs e) => SelectedGender = Gender.Male;
        private void FemaleButton_Click(object sender, RoutedEventArgs e) => SelectedGender = Gender.Female;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateVisualState();
        }
    }
}
