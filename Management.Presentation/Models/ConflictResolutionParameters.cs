using System;

namespace Management.Presentation.Models
{
    public class ConflictResolutionParameters
    {
        public string EntityName { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string LocalContent { get; set; } = string.Empty;
        public string RemoteContent { get; set; } = string.Empty;
        public string ConflictMessage { get; set; } = string.Empty;
    }
}
