using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;

namespace Management.Application.Services.History
{
    /// <summary>
    /// Restaurant-specific implementation of IHistoryProvider.
    /// Consolidates restaurant orders, payroll entries, and inventory purchases.
    /// </summary>
    public class RestaurantHistoryProvider : IHistoryProvider
    {
        private readonly IOrderService _orderService;
        private readonly IFinanceService _financeService;
        private readonly IInventoryService _inventoryService;

        public string SegmentName => "Restaurant";

        public RestaurantHistoryProvider(
            IOrderService orderService,
            IFinanceService financeService,
            IInventoryService inventoryService)
        {
            _orderService = orderService;
            _financeService = financeService;
            _inventoryService = inventoryService;
        }

        public async Task<IEnumerable<UnifiedHistoryEventDto>> GetHistoryAsync(Guid facilityId, DateTime startDate, DateTime endDate)
        {
            // Parallel fetch for all restaurant history sources
            var ordersTask = _orderService.GetOrdersByRangeAsync(facilityId, startDate, endDate);
            var payrollTask = _financeService.GetPayrollByRangeAsync(facilityId, startDate, endDate);
            var inventoryTask = _inventoryService.GetPurchasesByRangeAsync(facilityId, startDate, endDate);

            await Task.WhenAll(ordersTask, payrollTask, inventoryTask);

            var ordersResult = await ordersTask;
            var payrollResult = await payrollTask;
            var inventoryResult = await inventoryTask;

            var unifiedEvents = new List<UnifiedHistoryEventDto>();

            // 1. Map Restaurant Orders
            if (ordersResult.IsSuccess)
            {
                var finalOrders = ordersResult.Value.Where(o => o.Status == "Paid" || o.Status == "Completed");
                foreach (var order in finalOrders)
                {
                    bool isTakeout = string.IsNullOrEmpty(order.TableNumber) || order.TableNumber == "Takeout";
                    
                    unifiedEvents.Add(new UnifiedHistoryEventDto
                    {
                        Id = order.Id,
                        Timestamp = order.CompletedAt ?? order.CreatedAt,
                        Type = HistoryEventType.Order,
                        Title = isTakeout ? $"Takeout Order #{order.DailyOrderNumber}" : $"Table {order.TableNumber} (Ticket #{order.DailyOrderNumber})",
                        TitleLocalizationKey = isTakeout ? "Terminology.History.Order.TakeoutTitle" : "Terminology.History.Order.TableTitle",
                        TitleLocalizationArgs = isTakeout ? new[] { order.DailyOrderNumber.ToString() } : new[] { order.TableNumber ?? "", order.DailyOrderNumber.ToString() },
                        Details = string.Join(", ", order.Items.Select(i => $"{i.Quantity}x {i.Name}")),
                        DetailsLocalizationKey = "Terminology.History.Order.Details",
                        DetailsLocalizationArgs = new[] { string.Join(", ", order.Items.Select(i => $"{i.Quantity}x {i.Name}")) },
                        Amount = order.Total,
                        Metadata = isTakeout ? "Takeout" : $"Table {order.TableNumber}"
                    });
                }
            }

            // 2. Map Payroll Entries
            if (payrollResult.IsSuccess)
            {
                foreach (var entry in payrollResult.Value)
                {
                    unifiedEvents.Add(new UnifiedHistoryEventDto
                    {
                        Id = entry.Id,
                        Timestamp = entry.ProcessedAt ?? entry.PayPeriodEnd,
                        Type = HistoryEventType.Payroll,
                        Title = $"Payroll: {entry.StaffName}",
                        TitleLocalizationKey = "Terminology.History.Payroll.Title",
                        TitleLocalizationArgs = new[] { entry.StaffName },
                        Details = $"Period: {entry.PayPeriodStart:MMM dd} - {entry.PayPeriodEnd:MMM dd}",
                        DetailsLocalizationKey = "Terminology.History.Payroll.Details",
                        DetailsLocalizationArgs = new[] { entry.PayPeriodStart.ToString("MMM dd"), entry.PayPeriodEnd.ToString("MMM dd") },
                        Amount = entry.NetPay,
                        Metadata = entry.PaymentMethod
                    });
                }
            }

            // 3. Map Inventory Purchases
            foreach (var purchase in inventoryResult)
            {
                unifiedEvents.Add(new UnifiedHistoryEventDto
                {
                    Id = purchase.Id,
                    Timestamp = purchase.Date,
                    Type = HistoryEventType.Inventory,
                    Title = $"Inventory: {purchase.ResourceName}",
                    TitleLocalizationKey = "Terminology.History.Inventory.Title",
                    TitleLocalizationArgs = new[] { purchase.ResourceName },
                    Details = $"Bought {purchase.Quantity} {purchase.Unit} - Total: {purchase.TotalPrice:N2} DA (Note: {purchase.Note ?? "None"})",
                    DetailsLocalizationKey = "Terminology.History.Inventory.Details",
                    DetailsLocalizationArgs = new[] { purchase.Quantity.ToString(), purchase.Unit, purchase.Note ?? "None", purchase.TotalPrice.ToString("N2") },
                    Amount = purchase.TotalPrice,
                    Metadata = purchase.ResourceName
                });
            }

            return unifiedEvents.OrderByDescending(e => e.Timestamp);
        }
    }
}
