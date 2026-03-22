using System;
using Management.Domain.Enums;
using MediatR;

namespace Management.Application.Notifications
{
    public class SyncConflictNotification : INotification
    {
        public Guid EntityId { get; }
        public string EntityType { get; }
        public DateTime LocalUpdatedAt { get; }
        public DateTime ServerUpdatedAt { get; }
        public Guid FacilityId { get; }

        public SyncConflictNotification(Guid entityId, string entityType, DateTime localUpdatedAt, DateTime serverUpdatedAt, Guid facilityId)
        {
            EntityId = entityId;
            EntityType = entityType;
            LocalUpdatedAt = localUpdatedAt;
            ServerUpdatedAt = serverUpdatedAt;
            FacilityId = facilityId;
        }
    }
}
