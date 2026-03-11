using System;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a user's authentication session data.
    /// </summary>
    public class SessionData : Entity
    {

        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public Guid StaffId { get; set; }
        public Guid FacilityId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsExpiringSoon => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
    }
}
