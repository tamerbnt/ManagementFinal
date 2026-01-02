using Management.Application.DTOs;
using System;
using System.Collections.Generic;

namespace Management.Application.DTOs
{
    /// <summary>
    /// Represents a field-by-field comparison between local and server versions.
    /// </summary>
    public class ConflictComparisonDto
    {
        public string FieldName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string LocalValue { get; set; } = string.Empty;
        public string ServerValue { get; set; } = string.Empty;
        public bool IsDifferent { get; set; }
    }

    /// <summary>
    /// Contains all data needed to resolve a conflict.
    /// </summary>
    public class ConflictResolutionData
    {
        public Guid OutboxMessageId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public List<ConflictComparisonDto> Fields { get; set; } = new();
        public string LocalPayloadJson { get; set; } = string.Empty;
        public string ServerPayloadJson { get; set; } = string.Empty;
    }
}
