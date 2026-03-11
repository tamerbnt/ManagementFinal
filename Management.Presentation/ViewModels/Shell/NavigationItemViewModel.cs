using System;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels
{
    public class NavigationItemViewModel : ViewModelBase
    {
        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
        public string ResourceKey { get; }
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

        public NavigationItemViewModel(string displayName, string resourceKey, string iconKey, Type targetVmType)
        {
            DisplayName = displayName;
            ResourceKey = resourceKey;
            IconKey = iconKey;
            TargetViewModelType = targetVmType;
        }

        // Legacy/Full constructor if needed, or remove if unused locally
        public NavigationItemViewModel(string displayName, string resourceKey, string iconKey, Type targetVmType, object storeIgnored) 
            : this(displayName, resourceKey, iconKey, targetVmType)
        {
        }
    }
}
