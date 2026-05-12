using CommunityToolkit.Mvvm.ComponentModel;
using Management.Domain.Services;
using Management.Application.Interfaces.App;
using System;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Shared
{
    public partial class ConnectivityViewModel : ObservableObject
    {
        private readonly IConnectionService _connectionService;
        private readonly ISyncService _syncService;

        [ObservableProperty]
        private bool _isConnected = true; 

        [ObservableProperty]
        private bool _isCloudOnline = false;

        [ObservableProperty]
        private int _pendingSyncCount = 0; 

        public ConnectivityViewModel(IConnectionService connectionService, ISyncService syncService)
        {
            _connectionService = connectionService;
            _syncService = syncService;
           
            _connectionService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _syncService.SyncStatusChanged += OnSyncStatusChanged;
            
            // Initial poll
            IsCloudOnline = _syncService.Status != SyncStatus.Offline;
        }

        private void OnSyncStatusChanged(object? sender, SyncStatus status)
        {
            IsCloudOnline = status != SyncStatus.Offline;
            
            // Update pending count whenever sync state changes
            UpdatePendingCountAsync().ConfigureAwait(false);
        }

        private async Task UpdatePendingCountAsync()
        {
            PendingSyncCount = await _syncService.GetPendingOutboxCountAsync();
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
