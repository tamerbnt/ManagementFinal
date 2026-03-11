using System;
using System.Collections.ObjectModel;
using Management.Domain.Models;

namespace Management.Application.Abstractions
{
    public interface ISyncStore
    {
        bool IsSyncing { get; set; }
        DateTime? LastSyncTime { get; set; }
        bool HasConflicts { get; }
        
        void AddConflict(OutboxMessage message);
        void ResolveConflict(Guid messageId);
    }
}
