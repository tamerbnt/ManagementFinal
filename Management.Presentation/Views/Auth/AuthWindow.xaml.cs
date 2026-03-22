using System.Windows;
using System.Windows.Input;
using Management.Presentation.ViewModels.Onboarding;
using Management.Presentation.ViewModels;
using Management.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Management.Presentation.Views.Auth
{
    public partial class AuthWindow : Window
    {
        public AuthWindow()
        {
            InitializeComponent();
            
            // Initialize toast notification service
            var app = System.Windows.Application.Current as App;
            var toastService = app?.ServiceProvider?.GetService<IToastNotificationService>() as ToastNotificationService;
            toastService?.Initialize(ToastContainer);

            DataContextChanged += AuthWindow_DataContextChanged;
        }

        private void AuthWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is INotifyPropertyChanged newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
                UpdateCardProportions();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentView")
            {
                UpdateCardProportions();
            }
        }

        private void UpdateCardProportions()
        {
            if (DataContext == null) return;

            var propInfo = DataContext.GetType().GetProperty("CurrentView");
            if (propInfo != null)
            {
                var currentView = propInfo.GetValue(DataContext);
                if (currentView is LicenseEntryViewModel)
                {
                    CardBorder.VerticalAlignment = VerticalAlignment.Center;
                }
                else
                {
                    CardBorder.VerticalAlignment = VerticalAlignment.Stretch;
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}
