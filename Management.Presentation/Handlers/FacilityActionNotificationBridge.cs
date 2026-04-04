using CommunityToolkit.Mvvm.Messaging;
using Management.Application.Notifications;
using Management.Presentation.Messages;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Domain.Models;
using Management.Domain.Models.Salon;
using Management.Application.DTOs;
using Management.Presentation.Services.Salon;
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
        private readonly ISalonService _salonService;

        public FacilityActionNotificationBridge(
            IMessenger messenger, 
            IToastService toastService,
            IMemberService memberService,
            ISaleService saleService,
            IMediator mediator,
            ISalonService salonService)
        {
            _messenger = messenger;
            _toastService = toastService;
            _memberService = memberService;
            _saleService = saleService;
            _mediator = mediator;
            _salonService = salonService;
        }

        public async Task Handle(FacilityActionCompletedNotification notification, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"[BRIDGE] Handle called. ActionType={notification.ActionType}");
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
                                   notification.ActionType == "Checkout" ||
                                   notification.ActionType == "Appointment";

            if (isUndoableAction)
            {
                if (!string.IsNullOrEmpty(notification.EntityId))
                {
                    // Support comma-separated batch IDs (e.g., "id1,id2,id3")
                    var entityIdStrings = notification.EntityId.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    
                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[BRIDGE] Dispatcher.InvokeAsync called for ActionType={notification.ActionType}, Count={entityIdStrings.Length}");
                        
                        _toastService.ShowSuccess(notification.Message, async () =>
                        {
                            try 
                            {
                                foreach (var idString in entityIdStrings)
                                {
                                    if (!Guid.TryParse(idString, out var entityId)) continue;

                                    if (notification.ActionType == "Registration")
                                    {
                                        // Use the new atomic UndoRegistrationCommand to avoid AppDbContext concurrency issues
                                        // and ensure data integrity (Member + Sales deleted correctly in one transaction)
                                        await _mediator.Send(new Application.Features.Members.Commands.UndoRegistration.UndoRegistrationCommand(entityId, notification.FacilityId));
                                        
                                        _messenger.Send(new RefreshRequiredMessage<Member>(notification.FacilityId));
                                        _messenger.Send(new RefreshRequiredMessage<Sale>(notification.FacilityId));
                                    }
                                    else if (notification.ActionType == "Appointment")
                                    {
                                        await _salonService.CancelAppointmentAsync(entityId);
                                        _messenger.Send(new RefreshRequiredMessage<Appointment>(notification.FacilityId));
                                    }
                                    else
                                    {
                                        // Sale, QuickSale, Walk-In, Checkout
                                        await _saleService.CancelSaleAsync(entityId);
                                        _messenger.Send(new RefreshRequiredMessage<Sale>(notification.FacilityId));
                                    }
                                }
                                
                                string successMsg = entityIdStrings.Length > 1 ? $"{entityIdStrings.Length} actions undone successfully." : "Action undone successfully.";
                                _toastService.ShowInfo(successMsg);
                            }
                            catch (Exception ex)
                            {
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
            else if (notification.ActionType == "MemberUpdate" || 
                     notification.ActionType == "MemberRestore" || notification.ActionType == "InventoryPurchase")
            {
                // MemberDelete is intentionally excluded: MembersViewModel already shows
                // a ShowSuccess(message, undoAction) toast. Showing a second plain toast here
                // would evict it from the 1-slot queue before the user can click Undo.
                 _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        _toastService.ShowSuccess(notification.Message));
            }

            // 3. Trigger Refresh required messages
            if (notification.ActionType == "Sale" || 
                notification.ActionType == "QuickSale" || 
                notification.ActionType == "Walk-In" || 
                notification.ActionType == "Checkout" || 
                notification.ActionType == "Registration" ||    // FIX: Registration creates a sale — dashboard revenue cards must refresh
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
