using System;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels
{
    public class NavigationItemViewModel : ViewModelBase
    {
        public string DisplayName { get; }
        public string IconKey { get; }
        public Type TargetViewModelType { get; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        // Note: The Command is handled by the ItemsControl binding in MainView, 
        // which binds to MainViewModel.NavigateCommand with CommandParameter={Binding}

        public NavigationItemViewModel(string displayName, string iconKey, Type targetVmType, object storeIgnored)
        {
            DisplayName = displayName;
            IconKey = iconKey;
            TargetViewModelType = targetVmType;
        }
    }
}