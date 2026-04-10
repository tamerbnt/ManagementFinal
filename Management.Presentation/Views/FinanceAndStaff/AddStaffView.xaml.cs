using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Management.Presentation.Views.FinanceAndStaff
{
    public partial class AddStaffView : UserControl
    {
        private bool _passwordVisible = false;

        // Populated via Loaded events (bypasses ModalShellControl name-scope restriction)
        private PasswordBox? _staffPasswordBox;
        private TextBox? _passwordRevealBox;
        private Path? _eyeIconPath;
        private Border? _roleSelectionPill;
        private TranslateTransform? _rolePillTranslate;

        public AddStaffView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AnimateRolePill(isOwner: false);
        }

        // --- Loaded registrations ---
        private void StaffPasswordBox_Loaded(object sender, RoutedEventArgs e)
            => _staffPasswordBox = sender as PasswordBox;

        private void PasswordRevealBox_Loaded(object sender, RoutedEventArgs e)
            => _passwordRevealBox = sender as TextBox;

        private void EyeIconPath_Loaded(object sender, RoutedEventArgs e)
            => _eyeIconPath = sender as Path;

        private void RoleSelectionPill_Loaded(object sender, RoutedEventArgs e)
        {
            _roleSelectionPill = sender as Border;
            _rolePillTranslate = _roleSelectionPill?.RenderTransform as System.Windows.Media.TranslateTransform;
        }

        // --- Password Reveal ---
        private void TogglePasswordVisibility(object sender, RoutedEventArgs e)
        {
            if (_staffPasswordBox is null || _passwordRevealBox is null || _eyeIconPath is null) return;

            _passwordVisible = !_passwordVisible;

            if (_passwordVisible)
            {
                _passwordRevealBox.Text = _staffPasswordBox.Password;
                _staffPasswordBox.Visibility = Visibility.Collapsed;
                _passwordRevealBox.Visibility = Visibility.Visible;
                _eyeIconPath.Data = Geometry.Parse("M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24M1 1l22 22");
            }
            else
            {
                _staffPasswordBox.Password = _passwordRevealBox.Text;
                if (DataContext is ViewModels.Finance.AddStaffViewModel vm)
                    vm.NewStaff.Password = _staffPasswordBox.Password;
                _passwordRevealBox.Visibility = Visibility.Collapsed;
                _staffPasswordBox.Visibility = Visibility.Visible;
                _eyeIconPath.Data = Geometry.Parse("M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z");
            }
        }

        private void PasswordRevealBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_passwordVisible && DataContext is ViewModels.Finance.AddStaffViewModel vm)
                vm.NewStaff.Password = (sender as TextBox)?.Text ?? string.Empty;
        }

        private void StaffPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_passwordVisible && DataContext is ViewModels.Finance.AddStaffViewModel vm)
                vm.NewStaff.Password = (sender as PasswordBox)?.Password ?? string.Empty;
        }

        // --- Role Toggle ---
        private void AnimateRolePill(bool isOwner)
        {
            if (_roleSelectionPill is null || _rolePillTranslate is null) return;

            double panelWidth = _roleSelectionPill.Parent is FrameworkElement parent
                ? parent.ActualWidth - 8
                : 0;
            double pillWidth = panelWidth / 2;
            _roleSelectionPill.Width = pillWidth;

            var anim = new DoubleAnimation(isOwner ? pillWidth : 0, System.TimeSpan.FromSeconds(0.25))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            _rolePillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void RoleStaffButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Finance.AddStaffViewModel vm)
                vm.NewStaff.Role = "Staff";
            AnimateRolePill(isOwner: false);
        }

        private void RoleOwnerButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Finance.AddStaffViewModel vm)
                vm.NewStaff.Role = "Owner";
            AnimateRolePill(isOwner: true);
        }
    }
}
