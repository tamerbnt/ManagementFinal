using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Auth
{
    public partial class LicenseEntryView : UserControl
    {
        public LicenseEntryView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is LicenseEntryViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is LicenseEntryViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateVisualState(newVm.IsBusy);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LicenseEntryViewModel.IsBusy) && sender is LicenseEntryViewModel vm)
            {
                UpdateVisualState(vm.IsBusy);
            }
        }

        private void UpdateVisualState(bool isBusy)
        {
            VisualStateManager.GoToState(this, isBusy ? "Loading" : "Normal", true);
        }

        // Focus Trap / Management logic could be added here if needed for keyboard navigation
    }
}
