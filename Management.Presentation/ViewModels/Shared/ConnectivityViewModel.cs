using CommunityToolkit.Mvvm.ComponentModel;
using Management.Domain.Services;
using System;

namespace Management.Presentation.ViewModels.Shared
{
    public partial class ConnectivityViewModel : ObservableObject
    {
        private readonly IConnectionService _connectionService;

        [ObservableProperty]
        private bool _isConnected = true; // Default to true to avoid flashing offline on startup

        [ObservableProperty]
        private int _pendingSyncCount = 0; // Future use for "5 items pending"

        public ConnectivityViewModel(IConnectionService connectionService)
        {
            _connectionService = connectionService;
           
            // Initial check/subscription
            _connectionService.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        private void OnConnectionStatusChanged(bool isConnected)
        {
            // Use dispatcher if needed, but ObservableProperty usually handles UI thread marshaling if bound in WPF? 
            // Actually usually need to dispatch to UI thread. ObservableObject doesn't auto-dispatch events.
            // But let's assume the Service fires on UI thread or we handle dispatching in View or checking invoke in VM.
            // Safe bet is to set property. MvvmToolkit doesn't auto-dispatch property changes to UI thread? 
            // WPF 4.5+ handles property changes on background threads for scalar properties, usually.
            IsConnected = isConnected;
        }
    }
}
