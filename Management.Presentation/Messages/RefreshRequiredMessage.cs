using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace Management.Presentation.Messages
{
    /// <summary>
    /// A generic message used to notify ViewModels that a specific data type 
    /// has changed and requires a refresh (using the Dirty Flag pattern).
    /// </summary>
    /// <typeparam name="T">The type of data that changed (e.g., Sale, Member, Product).</typeparam>
    public class RefreshRequiredMessage<T> : ValueChangedMessage<Guid>
    {
        public RefreshRequiredMessage(Guid facilityId) : base(facilityId)
        {
        }
    }
}
