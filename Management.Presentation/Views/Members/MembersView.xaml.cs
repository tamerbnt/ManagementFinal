using System;
using System.Windows;
using System.Windows.Controls;
using Management.Presentation.ViewModels.Members;

namespace Management.Presentation.Views.Members
{
    /// <summary>
    /// Interaction logic for MembersView.xaml
    /// </summary>
    public partial class MembersView : UserControl
    {
        public MembersView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MembersViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is MembersViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // When a member is selected programmatically (like via Search in the top-bar)
            // we should ensure the ListBox scrolls to that member so it's visible.
            if (e.PropertyName == nameof(MembersViewModel.SelectedMember))
            {
                if (sender is MembersViewModel vm && vm.SelectedMember != null)
                {
                    // Dispatcher usage is CRITICAL for scrolling to items that are newly added
                    // or where the selection happened in the same tick as the view load.
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        MembersListBox.ScrollIntoView(vm.SelectedMember);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        // Constructor Injection for Dependency Injection
        public MembersView(MembersViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
