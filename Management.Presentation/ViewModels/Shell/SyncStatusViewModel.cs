using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Application.Interfaces.App;

namespace Management.Presentation.ViewModels.Shell
{
    public partial class SyncStatusViewModel : ObservableObject, IDisposable
    {
        private readonly ISyncService _syncService;
        private bool _disposed;

        [ObservableProperty]
        private string _statusText = "Idle";

        [ObservableProperty]
        private string _statusColor = "#808080"; // Gray

        [ObservableProperty]
        private bool _isSpinning = false;

        public SyncStatusViewModel(ISyncService syncService)
        {
            _syncService = syncService;
            _syncService.SyncStatusChanged += OnSyncStatusChanged;
            
            // Initial update
            UpdateUI(_syncService.Status);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _syncService.SyncStatusChanged -= OnSyncStatusChanged;
            _disposed = true;
        }

        private void OnSyncStatusChanged(object? sender, SyncStatus status)
        {
            // Ensure we update the UI on the correct thread asynchronously to prevent deadlocks
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateUI(status);
            });
        }

        private void UpdateUI(SyncStatus status)
        {
            switch (status)
            {
                case SyncStatus.Idle:
                    StatusText = "Idle";
                    StatusColor = "#4CAF50"; // Green
                    IsSpinning = false;
                    break;
                case SyncStatus.Syncing:
                    StatusText = "Synchronizing...";
                    StatusColor = "#2196F3"; // Blue
                    IsSpinning = true;
                    break;
                case SyncStatus.Offline:
                    StatusText = "Offline";
                    StatusColor = "#FF9800"; // Orange
                    IsSpinning = false;
                    break;
                case SyncStatus.Error:
                    StatusText = "Sync Error";
                    StatusColor = "#F44336"; // Red
                    IsSpinning = false;
                    break;
            }
        }
    }
}
