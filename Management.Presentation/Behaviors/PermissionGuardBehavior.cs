using System.Windows;
using Microsoft.Xaml.Behaviors;
using System.Linq;

namespace Management.Presentation.Behaviors
{
    public class PermissionGuardBehavior : Behavior<UIElement>
    {
        public static readonly DependencyProperty RequiredRolesProperty =
            DependencyProperty.Register("RequiredRoles", typeof(string), typeof(PermissionGuardBehavior), 
                new PropertyMetadata(string.Empty, OnRequiredRolesChanged));

        public string RequiredRoles
        {
            get => (string)GetValue(RequiredRolesProperty);
            set => SetValue(RequiredRolesProperty, value);
        }

        public static readonly DependencyProperty UserRoleProperty =
            DependencyProperty.Register("UserRole", typeof(string), typeof(PermissionGuardBehavior), 
                new PropertyMetadata(string.Empty, OnUserRoleChanged));

        public string UserRole
        {
            get => (string)GetValue(UserRoleProperty);
            set => SetValue(UserRoleProperty, value);
        }

        private static void OnRequiredRolesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PermissionGuardBehavior)d).UpdateVisibility();
        }

        private static void OnUserRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PermissionGuardBehavior)d).UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (AssociatedObject == null) return;

            if (string.IsNullOrEmpty(RequiredRoles))
            {
                AssociatedObject.Visibility = Visibility.Visible;
                return;
            }

            var allowedRoles = RequiredRoles.Split(',').Select(r => r.Trim());
            if (allowedRoles.Contains(UserRole))
            {
                AssociatedObject.Visibility = Visibility.Visible;
            }
            else
            {
                AssociatedObject.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            UpdateVisibility();
        }
    }
}
