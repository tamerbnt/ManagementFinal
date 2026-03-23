using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace Management.Presentation.Messages
{
    public class FacilityActionCompletedMessage : ValueChangedMessage<Guid>
    {
        public string ActionType { get; }
        public string DisplayName { get; }
        public string Message { get; }
        public string? EntityId { get; }

        public FacilityActionCompletedMessage(Guid facilityId, string actionType, string displayName, string message, string? entityId = null) : base(facilityId)
        {
            ActionType = actionType;
            DisplayName = displayName;
            Message = message;
            EntityId = entityId;
        }
    }
}
