using System;
using Management.Domain.Common;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a pending registration (legacy/deprecated - use Registration instead).
    /// </summary>
    [Obsolete("Use Registration model instead")]
    public class PendingRegistration : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public bool IsProcessed { get; set; }

        public PendingRegistration() : base()
        {
            SubmittedAt = DateTime.UtcNow;
            IsProcessed = false;
        }
    }
}
