using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class AccessEvent : Entity
    {
        public DateTime Timestamp { get; private set; }
        public Guid TurnstileId { get; private set; }
        public string CardId { get; private set; }
        public bool IsAccessGranted { get; private set; }
        public AccessStatus AccessStatus { get; private set; } // Granted, Denied, Locked
        public string FailureReason { get; private set; } // e.g. "Expired Membership"

        private AccessEvent(Guid id, Guid turnstileId, string cardId, bool isAccessGranted, AccessStatus accessStatus, string failureReason) : base(id)
        {
            Timestamp = DateTime.UtcNow;
            TurnstileId = turnstileId;
            CardId = cardId;
            IsAccessGranted = isAccessGranted;
            AccessStatus = accessStatus;
            FailureReason = failureReason;
        }

        private AccessEvent() { CardId = string.Empty; FailureReason = string.Empty; }

        public static AccessEvent Create(Guid turnstileId, string cardId, bool granted, AccessStatus status, string reason = "")
        {
            return new AccessEvent(Guid.NewGuid(), turnstileId, cardId, granted, status, reason);
        }
    }
}