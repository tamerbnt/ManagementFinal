using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models.Resilience
{
    public class OfflineAction : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public string EntityType { get; set; } = string.Empty;
        public OfflineActionType ActionType { get; set; }
        public string Payload { get; set; } = string.Empty; // JSON serialized entity or delta
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
    }
}
