using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Interfaces;
using Management.Domain.Common;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Microsoft.Extensions.Logging;

namespace Management.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ITableRepository _tableRepository;
        private readonly IPrinterService _printerService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IOrderRepository orderRepository, ITableRepository tableRepository, IPrinterService printerService, ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _tableRepository = tableRepository ?? throw new ArgumentNullException(nameof(tableRepository));
            _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<Guid>> StartOrderAsync(Guid? tableId, string? tableNumber, Guid tenantId, Guid facilityId, int partySize = 1)
        {
            try
            {
                var dailyNumber = 0;
                if (_orderRepository is IRestaurantOrderRepository restaurantRepo)
                {
                    dailyNumber = await restaurantRepo.GetNextDailyOrderNumberAsync(facilityId);
                }

                string section = "Takeout";
                string finalTableNumber = tableNumber ?? "Takeout";

                if (tableId.HasValue)
                {
                    var table = await _tableRepository.GetByIdAsync(tableId.Value);
                    if (table != null)
                    {
                        section = table.Section;
                        finalTableNumber = table.Label;
                    }
                }
                else if (!string.IsNullOrEmpty(tableNumber) && tableNumber != "Takeout")
                {
                    // Fallback to label lookup if ID is not provided (legacy or specific use case)
                    var tables = await _tableRepository.GetByFacilityIdAsync(facilityId);
                    var table = tables.FirstOrDefault(t => t.Label == tableNumber);
                    if (table != null)
                    {
                        section = table.Section;
                    }
                }

                var order = new RestaurantOrder
                {
                    Id = Guid.NewGuid(),
                    TableId = tableId,
                    TableNumber = finalTableNumber ?? "Takeout",
                    Section = section ?? "Takeout",
                    TenantId = tenantId,
                    FacilityId = facilityId,
                    DailyOrderNumber = dailyNumber,
                    Status = OrderStatus.Pending,
                    PartySize = partySize
                };

                await _orderRepository.AddAsync(order);

                return Result.Success(order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start order for table {Table}", tableNumber);
                return Result.Failure<Guid>(new Error("Order.StartFailed", ex.Message));
            }
        }

        public async Task<Result> AddItemToOrderAsync(Guid orderId, string itemName, decimal price, int quantity)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null) return Result.Failure(new Error("Order.NotFound", "Order not found"));

                var existingItem = order.Items.FirstOrDefault(i => i.Name == itemName);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    order.Items.Add(new OrderItem
                    {
                        Id = Guid.Empty,
                        TenantId = order.TenantId,
                        FacilityId = order.FacilityId,
                        RestaurantOrderId = order.Id,
                        Name = itemName,
                        Price = price,
                        Quantity = quantity
                    });
                }

                order.CalculateTotal();
                await _orderRepository.UpdateAsync(order);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add item {Item} to order {OrderId}", itemName, orderId);
                return Result.Failure(new Error("Order.AddItemFailed", ex.Message));
            }
        }

        public async Task<Result> UpdateItemQuantityAsync(Guid orderId, string itemName, int newQuantity)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null) return Result.Failure(new Error("Order.NotFound", "Order not found"));

                var item = order.Items.FirstOrDefault(i => i.Name == itemName);
                if (item == null) return Result.Failure(new Error("Order.ItemNotFound", "Item not found in order"));

                if (newQuantity <= 0)
                {
                    order.Items.Remove(item);
                }
                else
                {
                    item.Quantity = newQuantity;
                }

                order.CalculateTotal();
                await _orderRepository.UpdateAsync(order);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update quantity for item {Item} in order {OrderId}", itemName, orderId);
                return Result.Failure(new Error("Order.UpdateQuantityFailed", ex.Message));
            }
        }

        public async Task<Result> RemoveItemFromOrderAsync(Guid orderId, string itemName)
        {
            return await UpdateItemQuantityAsync(orderId, itemName, 0);
        }

        public async Task<Result> SendToKitchenAsync(Guid orderId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null) return Result.Failure(new Error("Order.NotFound", "Order not found"));

                order.Status = OrderStatus.InKitchen;
                await _orderRepository.UpdateAsync(order);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order {OrderId} to kitchen", orderId);
                return Result.Failure(new Error("Order.StatusUpdateFailed", ex.Message));
            }
        }

        public async Task<Result> CompleteOrderAsync(Guid orderId)
        {
             try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null) return Result.Failure(new Error("Order.NotFound", "Order not found"));

                // 1. Mark order as Paid
                order.Status = OrderStatus.Paid;
                order.DeliveredAt = DateTime.UtcNow;
                order.CompletedAt = DateTime.UtcNow;
                await _orderRepository.UpdateAsync(order);

                // 2. Sync Table status
                if (order.TableId.HasValue)
                {
                    var table = await _tableRepository.GetByIdAsync(order.TableId.Value);
                    if (table != null)
                    {
                        table.Status = TableStatus.Available;
                        table.CurrentOccupancy = 0;
                        await _tableRepository.UpdateAsync(table);
                        _logger.LogInformation("Table {TableNumber} reset to Available after order {OrderId} completion", order.TableNumber, orderId);
                    }
                }
                else if (!string.IsNullOrEmpty(order.TableNumber) && order.TableNumber != "Takeout")
                {
                    // Fallback to label lookup if ID is missing (legacy orders)
                    var facilityTables = await _tableRepository.GetByFacilityIdAsync(order.FacilityId);
                    var table = facilityTables.FirstOrDefault(t => t.Label == order.TableNumber);
                    
                    if (table != null)
                    {
                        table.Status = TableStatus.Available;
                        table.CurrentOccupancy = 0;
                        await _tableRepository.UpdateAsync(table);
                        _logger.LogInformation("Table {TableNumber} reset to Available after order {OrderId} completion (via Label)", order.TableNumber, orderId);
                    }
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete order {OrderId}", orderId);
                return Result.Failure(new Error("Order.StatusUpdateFailed", ex.Message));
            }
        }

        public async Task<Result> CancelOrderAsync(Guid orderId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null) return Result.Failure(new Error("Order.NotFound", "Order not found"));

                order.Status = OrderStatus.Cancelled;
                await _orderRepository.UpdateAsync(order);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
                return Result.Failure(new Error("Order.StatusUpdateFailed", ex.Message));
            }
        }

        public async Task<Result<IEnumerable<RestaurantOrderDto>>> GetActiveOrdersAsync(Guid? facilityId = null)
        {
            try
            {
                var orders = await _orderRepository.GetActiveOrdersAsync(facilityId);
                return Result.Success<IEnumerable<RestaurantOrderDto>>(orders.Select(MapToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active orders");
                return Result.Failure<IEnumerable<RestaurantOrderDto>>(new Error("Order.LoadFailed", ex.Message));
            }
        }

        public async Task<Result<RestaurantOrderDto?>> GetOrderByTableIdAsync(Guid tableId)
        {
            try
            {
                var order = await _orderRepository.GetActiveOrderByTableIdAsync(tableId);
                return Result.Success<RestaurantOrderDto?>(order != null ? MapToDto(order) : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order for table ID {TableId}", tableId);
                return Result.Failure<RestaurantOrderDto?>(new Error("Order.LoadFailed", ex.Message));
            }
        }

        public async Task<Result<RestaurantOrderDto?>> GetOrderByTableAsync(string tableNumber)
        {
             try
            {
                var order = await _orderRepository.GetActiveOrderByTableAsync(tableNumber);
                return Result.Success<RestaurantOrderDto?>(order != null ? MapToDto(order) : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order for table {Table}", tableNumber);
                return Result.Failure<RestaurantOrderDto?>(new Error("Order.LoadFailed", ex.Message));
            }
        }

        public async Task<Result<RestaurantOrderDto?>> GetOrderByIdAsync(Guid orderId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                return Result.Success<RestaurantOrderDto?>(order != null ? MapToDto(order) : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order {OrderId}", orderId);
                return Result.Failure<RestaurantOrderDto?>(new Error("Order.LoadFailed", ex.Message));
            }
        }

        public async Task<Result> PrintOrderAsync(Guid orderId)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(orderId);
                if (order == null) return Result.Failure(new Error("Order.NotFound", "Order not found"));

                await _printerService.PrintOrderAsync(order);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to print order {OrderId}", orderId);
                return Result.Failure(new Error("Order.PrintFailed", ex.Message));
            }
        }

        public async Task<Result<decimal>> GetTodayRevenueAsync(Guid facilityId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var orders = await _orderRepository.GetTodayCompletedOrdersAsync(facilityId, startDate, endDate);
                return Result.Success(orders.Sum(o => o.Total));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get today revenue for facility {FacilityId}", facilityId);
                return Result.Failure<decimal>(new Error("Order.RevenueCalculationFailed", ex.Message));
            }
        }

        public async Task<Result<IEnumerable<RestaurantOrderDto>>> GetRecentActivitiesAsync(Guid facilityId, int count)
        {
            try
            {
                var orders = await _orderRepository.GetRecentOrdersAsync(facilityId, count);
                return Result.Success(orders.Select(MapToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent activities for facility {FacilityId}", facilityId);
                return Result.Failure<IEnumerable<RestaurantOrderDto>>(new Error("Order.ActivityLoadFailed", ex.Message));
            }
        }

        public async Task<Result<IEnumerable<RestaurantOrderDto>>> GetOrdersByRangeAsync(Guid facilityId, DateTime start, DateTime end)
        {
            try
            {
                var orders = await _orderRepository.GetOrdersByRangeAsync(facilityId, start, end);
                return Result.Success(orders.Select(MapToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get orders by range for facility {FacilityId}", facilityId);
                return Result.Failure<IEnumerable<RestaurantOrderDto>>(new Error("Order.LoadByRangeFailed", ex.Message));
            }
        }

        private RestaurantOrderDto MapToDto(RestaurantOrder order)
        {
            return new RestaurantOrderDto
            {
                Id = order.Id,
                TableId = order.TableId,
                TableNumber = order.TableNumber ?? "Unknown",
                Section = order.Section ?? "",
                Status = order.Status.ToString(),
                DailyOrderNumber = order.DailyOrderNumber,
                PartySize = order.PartySize,
                ServerName = string.Empty,
                Subtotal = order.Subtotal,
                Tax = order.Tax,
                Total = order.Total,
                CreatedAt = order.CreatedAt,
                CompletedAt = order.CompletedAt,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Name = i.Name,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList()
            };
        }
    }
}
