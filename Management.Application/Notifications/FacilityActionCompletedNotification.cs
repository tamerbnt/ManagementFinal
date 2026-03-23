using MediatR;

namespace Management.Application.Notifications
{
    /// <summary>
    /// Notification sent when a significant facility operation is completed (Walk-in, Sale, Registration, etc.).
    /// </summary>
    /// <param name="ActionType">Type of action: "WalkIn", "QuickSale", "Registration", etc.</param>
    /// <param name="DisplayName">User-friendly name of the entity (e.g., "Guest", "Coca-Cola")</param>
    /// <param name="Message">Success message</param>
    public record FacilityActionCompletedNotification(
        Guid FacilityId,
        string ActionType, 
        string DisplayName, 
        string Message,
        string? EntityId = null) : INotification;
}
