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
        private readonly IMediator _mediator;

        public FacilityActionNotificationBridge(
            IMessenger messenger, 
            IToastService toastService,
            IMemberService memberService,
            ISaleService saleService,
            IMediator mediator)
        {
            _messenger = messenger;
            _toastService = toastService;
            _memberService = memberService;
            _saleService = saleService;
            _mediator = mediator;
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
                                   notification.ActionType == "Walk-In" ||
                                   notification.ActionType == "Checkout";

            if (isUndoableAction)
            {
                if (!string.IsNullOrEmpty(notification.EntityId) && Guid.TryParse(notification.EntityId, out var entityId))
                {
                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _toastService.ShowSuccess(notification.Message, async () =>
                        {
                            try 
                            {
                                if (notification.ActionType == "Registration")
                                {
                                    // Use the new atomic UndoRegistrationCommand to avoid AppDbContext concurrency issues
                                    // and ensure data integrity (Member + Sales deleted correctly in one transaction)
                                    await _mediator.Send(new Application.Features.Members.Commands.UndoRegistration.UndoRegistrationCommand(entityId, notification.FacilityId));
                                    
                                    _messenger.Send(new RefreshRequiredMessage<Member>(notification.FacilityId));
                                    _messenger.Send(new RefreshRequiredMessage<Sale>(notification.FacilityId));
                                }
                                else
                                {
                                    // Sale, QuickSale, Walk-In, Checkout
                                    await _saleService.CancelSaleAsync(entityId);
                                    _messenger.Send(new RefreshRequiredMessage<Sale>(notification.FacilityId));
                                }
                                _toastService.ShowInfo("Action undone successfully.");
                            }
                            catch (Exception ex)
                            {
                                // Log the error since it might be a DbUpdateException with deep inner exceptions
                                // Note: _logger is not available here, but we can use System.Diagnostics.Debug or a static logger if available.
                                // Actually, this class has access to dependencies. I'll add ILogger to it if missing.
                                Console.WriteLine($"[Undo Error] {ex}");
                                _toastService.ShowError($"Undo failed: {ex.Message}");
                            }
                        }, "Undo");
                    });
                }
                else
                {
                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        _toastService.ShowSuccess(notification.Message));
                }
            }
            else if (notification.ActionType == "MemberUpdate" || notification.ActionType == "MemberDelete" || 
                     notification.ActionType == "MemberRestore" || notification.ActionType == "InventoryPurchase")
            {
                 _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        _toastService.ShowSuccess(notification.Message));
            }

            // 3. Trigger Refresh required messages
            if (notification.ActionType == "Sale" || 
                notification.ActionType == "QuickSale" || 
                notification.ActionType == "Walk-In" || 
                notification.ActionType == "Checkout" || 
                notification.ActionType.Contains("Service"))
            {
                _messenger.Send(new RefreshRequiredMessage<Sale>(notification.FacilityId));
            }
            
            if (notification.ActionType == "Registration" || 
                notification.ActionType == "MemberUpdate" || 
                notification.ActionType == "MemberDelete" || 
                notification.ActionType == "MemberRestore")
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
