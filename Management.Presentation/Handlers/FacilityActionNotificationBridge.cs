using CommunityToolkit.Mvvm.Messaging;
using Management.Application.Notifications;
using Management.Presentation.Messages;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Domain.Models;
using Management.Application.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Management.Presentation.Handlers
{
    public class FacilityActionNotificationBridge : INotificationHandler<FacilityActionCompletedNotification>
    {
        private readonly IMessenger _messenger;
        private readonly IToastService _toastService;
        private readonly IMemberService _memberService;
        private readonly ISaleService _saleService;

        public FacilityActionNotificationBridge(
            IMessenger messenger, 
            IToastService toastService,
            IMemberService memberService,
            ISaleService saleService)
        {
            _messenger = messenger;
            _toastService = toastService;
            _memberService = memberService;
            _saleService = saleService;
        }

        public async Task Handle(FacilityActionCompletedNotification notification, CancellationToken cancellationToken)
        {
            // 1. Forward the MediatR notification to the UI Messenger
            _messenger.Send(new FacilityActionCompletedMessage(
                notification.FacilityId,
                notification.ActionType,
                notification.DisplayName,
                notification.Message,
                notification.EntityId
            ));

            // 2. Trigger Toast for significant actions
            bool isUndoableAction = notification.ActionType == "Registration" || 
                                   notification.ActionType == "QuickSale" || 
                                   notification.ActionType == "Sale" || 
                                   notification.ActionType == "Walk-In";

            if (isUndoableAction)
            {
                if (!string.IsNullOrEmpty(notification.EntityId) && Guid.TryParse(notification.EntityId, out var entityId))
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _toastService.ShowSuccess(notification.Message, async () =>
                        {
                            try 
                            {
                                if (notification.ActionType == "Registration")
                                {
                                    await _memberService.DeleteMembersAsync(notification.FacilityId, new List<Guid> { entityId });
                                }
                                else
                                {
                                    // Sale, QuickSale, Walk-In
                                    await _saleService.CancelSaleAsync(entityId);
                                }
                                _toastService.ShowInfo("Action undone successfully.");
                            }
                            catch (Exception ex)
                            {
                                _toastService.ShowError($"Undo failed: {ex.Message}");
                            }
                        }, "Undo");
                    });
                }
                else
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        _toastService.ShowSuccess(notification.Message));
                }
            }
            else if (notification.ActionType == "MemberUpdate" || notification.ActionType == "InventoryPurchase")
            {
                 await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        _toastService.ShowSuccess(notification.Message));
            }

            // 3. Trigger Refresh required messages
            if (notification.ActionType == "Sale" || notification.ActionType == "QuickSale" || notification.ActionType == "Walk-In" || notification.ActionType.Contains("Service"))
            {
                _messenger.Send(new RefreshRequiredMessage<Sale>(notification.FacilityId));
            }
            
            if (notification.ActionType == "Registration" || notification.ActionType == "MemberUpdate")
            {
                _messenger.Send(new RefreshRequiredMessage<Member>(notification.FacilityId));
            }

            if (notification.ActionType == "InventoryPurchase")
            {
                _messenger.Send(new RefreshRequiredMessage<InventoryPurchaseDto>(notification.FacilityId));
            }
        }
    }
}
