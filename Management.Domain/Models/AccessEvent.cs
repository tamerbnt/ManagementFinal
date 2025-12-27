using System;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    /// <summary>
    /// An immutable log entry for every entry/exit attempt.
    /// </summary>
    public class AccessEvent : Entity
    {
        public DateTime Timestamp { get; set; }

        // Who
        public Guid? MemberId { get; set; } // Nullable if unknown card
        public string MemberNameSnapshot { get; set; } // Snapshot in case member is deleted later
        public string CardId { get; set; } // The raw card ID scanned

        // Where
        public FacilityType FacilityType { get; set; }
        public Guid? TurnstileId { get; set; }

        // Result
        public bool IsAccessGranted { get; set; }
        public AccessStatus AccessStatus { get; set; } // Granted, Denied, Locked
        public string FailureReason { get; set; } // e.g. "Expired Membership"
    }
}