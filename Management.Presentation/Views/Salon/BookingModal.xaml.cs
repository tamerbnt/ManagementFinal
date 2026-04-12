using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Management.Presentation.Views.Salon
{
    public partial class BookingModal : Window
    {
        private Border? _selectionPill;
        private TranslateTransform? _pillTranslate;

        public BookingModal()
        {
            InitializeComponent();
        }

        private void SelectionPill_Loaded(object sender, RoutedEventArgs e)
        {
            _selectionPill = sender as Border;
            if (_selectionPill != null)
            {
                _pillTranslate = _selectionPill.RenderTransform as TranslateTransform;
                // Initial state
                UpdatePillPosition(true);
            }
        }

        private void ExistingMember_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BookingViewModel vm)
            {
                vm.IsExistingClientMode = true;
                UpdatePillPosition(true);
            }
        }

        private void QuickRegister_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BookingViewModel vm)
            {
                vm.IsExistingClientMode = false;
                UpdatePillPosition(false);
            }
        }

        private void UpdatePillPosition(bool isExisting)
        {
            if (_selectionPill == null || _pillTranslate == null) return;

            double panelWidth = (_selectionPill.Parent as FrameworkElement)?.ActualWidth ?? 0;
            if (panelWidth == 0) panelWidth = 280; // Fallback or wait for layout

            double activeWidth = (panelWidth - 8) / 2;
            _selectionPill.Width = activeWidth;

            double targetX = isExisting ? 0 : activeWidth;

            var anim = new DoubleAnimation(targetX, TimeSpan.FromSeconds(0.25))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            _pillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }
    }
}
