using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class DeviceStatusViewModel : ObservableObject
    {
        private readonly IRelayCommand<DeviceStatusViewModel> _testCommand;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _type = string.Empty;

        [ObservableProperty]
        private string _lastError = string.Empty;

        [ObservableProperty]
        private bool _isTesting;

        public IRelayCommand TestCommand => _testCommand;

        public DeviceStatusViewModel(DeviceStatus status, IRelayCommand<DeviceStatusViewModel> testCommand)
        {
            _name = status.Name;
            _isConnected = status.IsConnected;
            _type = status.Type;
            _lastError = status.LastError;
            _testCommand = testCommand;
        }

        public void Update(DeviceStatus status)
        {
            IsConnected = status.IsConnected;
            LastError = status.LastError;
        }
    }
}
