using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace Management.Presentation.Messages
{
    public class TableStatusChangedMessage : ValueChangedMessage<Guid>
    {
        public TableStatusChangedMessage(Guid facilityId) : base(facilityId) { }
    }
}
