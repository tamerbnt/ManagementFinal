using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a user's authentication session data persisted to disk (session.dat).
    /// </summary>
    public class SessionData : Entity
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public Guid StaffId { get; set; }
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// True when the user authenticated via local BCrypt fallback (no internet).
        /// Cloud sync is intentionally disabled for offline sessions — no error toast.
        /// </summary>
        public bool IsOfflineSession { get; set; } = false;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsExpiringSoon => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
    }
}
