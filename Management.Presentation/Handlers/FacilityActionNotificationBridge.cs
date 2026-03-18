using CommunityToolkit.Mvvm.Messaging;
using Management.Application.Notifications;
using Management.Presentation.Messages;
using Management.Application.Services;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Presentation.Handlers
{
    public class FacilityActionNotificationBridge : INotificationHandler<FacilityActionCompletedNotification>
    {
        public Task Handle(FacilityActionCompletedNotification notification, CancellationToken cancellationToken)
        {
            // Forward the MediatR notification to the UI Messenger
            WeakReferenceMessenger.Default.Send(new FacilityActionCompletedMessage(
                notification.FacilityId,
                notification.ActionType,
                notification.DisplayName,
                notification.Message
            ));

            // Also send RefreshRequiredMessages for relevant actions to trigger UI updates in lists/history
            if (notification.ActionType == "QuickSale" || notification.ActionType == "Walk-In" || notification.ActionType.Contains("Service"))
            {
                WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<Management.Domain.Models.Sale>(notification.FacilityId));
            }
            
            if (notification.ActionType == "Registration" || notification.ActionType == "MemberUpdate")
            {
                // Note: We avoid sending this for "Access" here because Access 
                // already sends FacilityActionCompletedMessage which triggers the same logic.
                WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<Management.Domain.Models.Member>(notification.FacilityId));
            }

            if (notification.ActionType == "InventoryPurchase")
            {
                WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<InventoryPurchaseDto>(notification.FacilityId));
            }
            
            return Task.CompletedTask;
        }
    }
}
