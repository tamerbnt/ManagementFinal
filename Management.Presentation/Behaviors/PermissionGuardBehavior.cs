using System.Windows;
using Microsoft.Xaml.Behaviors;
using System.Linq;

namespace Management.Presentation.Behaviors
{
    public class PermissionGuardBehavior : Behavior<UIElement>
    {
        public static readonly DependencyProperty RequiredPermissionProperty =
            DependencyProperty.Register("RequiredPermission", typeof(string), typeof(PermissionGuardBehavior), 
                new PropertyMetadata(string.Empty, OnRequiredPermissionChanged));

        public string RequiredPermission
        {
            get => (string)GetValue(RequiredPermissionProperty);
            set => SetValue(RequiredPermissionProperty, value);
        }

        private static void OnRequiredPermissionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PermissionGuardBehavior)d).UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (AssociatedObject == null) return;

            if (string.IsNullOrEmpty(RequiredPermission))
            {
                AssociatedObject.Visibility = Visibility.Visible;
                return;
            }

            // In design mode, always show
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                AssociatedObject.Visibility = Visibility.Visible;
                return;
            }

            try 
            {
                var app = System.Windows.Application.Current as App;
                if (app?.ServiceProvider != null)
                {
                    var accountStore = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Management.Application.Stores.AccountStore>(app.ServiceProvider);
                    if (accountStore != null && accountStore.HasPermission(RequiredPermission))
                    {
                        AssociatedObject.Visibility = Visibility.Visible;
                        return;
                    }
                }
            }
            catch 
            {
                // Fallback for missing services or unexpected errors
            }

            AssociatedObject.Visibility = Visibility.Collapsed;
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            UpdateVisibility();
        }
    }
}
