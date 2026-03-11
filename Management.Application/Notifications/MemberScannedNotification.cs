using System;
using Management.Application.DTOs;
using Management.Domain.Enums;
using MediatR;

namespace Management.Application.Notifications
{
    /// <summary>
    /// Notification published when a member scans their card at a turnstile.
    /// </summary>
    public record MemberScannedNotification(
        Guid FacilityId,
        MemberDto? Member,
        string CardId,
        bool IsAccessGranted,
        AccessStatus Status,
        string? FailureReason = null) : INotification;
}
