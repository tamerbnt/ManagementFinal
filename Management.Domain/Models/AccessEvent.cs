using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class AccessEvent : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public DateTime Timestamp { get; private set; }
        public Guid TurnstileId { get; private set; }
        public string CardId { get; private set; }
        public string TransactionId { get; private set; } // Hardware-generated ID from scan (e.g., "CD11301121")
        public bool IsAccessGranted { get; private set; }
        public AccessStatus AccessStatus { get; private set; } // Granted, Denied, Locked
        public ScanDirection Direction { get; private set; }
        public string FailureReason { get; private set; } // e.g. "Expired Membership"

        private AccessEvent(Guid id, Guid turnstileId, string cardId, string transactionId, bool isAccessGranted, AccessStatus accessStatus, ScanDirection direction, string failureReason) : base(id)
        {
            Timestamp = DateTime.UtcNow;
            TurnstileId = turnstileId;
            CardId = cardId;
            TransactionId = transactionId;
            IsAccessGranted = isAccessGranted;
            AccessStatus = accessStatus;
            Direction = direction;
            FailureReason = failureReason;
        }

        private AccessEvent() { CardId = string.Empty; TransactionId = string.Empty; FailureReason = string.Empty; }

        public static AccessEvent Create(Guid turnstileId, string cardId, string transactionId, bool granted, AccessStatus status, ScanDirection direction, string reason = "")
        {
            return new AccessEvent(Guid.NewGuid(), turnstileId, cardId, transactionId, granted, status, direction, reason);
        }
    }
}
