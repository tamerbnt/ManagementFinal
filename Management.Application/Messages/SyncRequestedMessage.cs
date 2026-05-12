using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace Management.Application.Messages
{
    /// <summary>
    /// Message sent to trigger an immediate background synchronization cycle
    /// (e.g., when entering Remote Mode or after a critical data change).
    /// </summary>
    public class SyncRequestedMessage : ValueChangedMessage<Guid>
    {
        public SyncRequestedMessage(Guid facilityId) : base(facilityId)
        {
        }
    }
}
