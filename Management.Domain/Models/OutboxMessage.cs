using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class OutboxMessage : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Insert, Update, Delete
        public string ContentJson { get; set; } = string.Empty;
        public Guid? CreatedBy { get; set; } // User who performed the action
        public bool IsProcessed { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int ErrorCount { get; set; }
        public bool IsConflict { get; set; }
        public bool IsDeadLetter { get; set; }
        public string? LastError { get; set; }
        public string? ServerPayload { get; set; } // Snapshot of server state for conflict resolution
    }
}
