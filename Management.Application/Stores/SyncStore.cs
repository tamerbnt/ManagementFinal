using System;
using System.Collections.ObjectModel;
using System.Linq;
using Management.Domain.Models;

using Management.Domain.Interfaces;

namespace Management.Application.Stores
{
    public class SyncStore : IStateResettable
    {
        public void ResetState()
        {
            _isSyncing = false;
            _lastSyncTime = null;
            ConflictedMessages.Clear();
            SyncStatusChanged?.Invoke();
        }

        private bool _isSyncing;
        public bool IsSyncing
        {
            get => _isSyncing;
            set
            {
                _isSyncing = value;
                SyncStatusChanged?.Invoke();
            }
        }

        private DateTime? _lastSyncTime;
        public DateTime? LastSyncTime
        {
            get => _lastSyncTime;
            set
            {
                _lastSyncTime = value;
                SyncStatusChanged?.Invoke();
            }
        }

        public ObservableCollection<OutboxMessage> ConflictedMessages { get; } = new ObservableCollection<OutboxMessage>();

        public bool HasConflicts => ConflictedMessages.Count > 0;

        public event Action? SyncStatusChanged;
        public event Action<OutboxMessage>? ConflictDetected;

        public void AddConflict(OutboxMessage message)
        {
            if (!ConflictedMessages.Any(m => m.Id == message.Id))
            {
                ConflictedMessages.Add(message);
                SyncStatusChanged?.Invoke();
                ConflictDetected?.Invoke(message);
            }
        }

        public void ResolveConflict(Guid messageId)
        {
            var message = ConflictedMessages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                ConflictedMessages.Remove(message);
                SyncStatusChanged?.Invoke();
            }
        }
    }
}
