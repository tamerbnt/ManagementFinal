using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models.Resilience
{
    public enum OfflineActionType
    {
        Create,
        Update,
        Delete,
        Sync
    }

    public class OfflineAction : Entity
    {

        public string EntityType { get; set; } = string.Empty;
        public OfflineActionType ActionType { get; set; }
        public string Payload { get; set; } = string.Empty; // JSON serialized entity or delta
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
    }
}
