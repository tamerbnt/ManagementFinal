using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Management.Domain.Models.Restaurant;
using Management.Presentation.Extensions;
using Management.Presentation.Services.Restaurant;
using Management.Presentation.Services;

namespace Management.Presentation.Views.Restaurant
{
    public class OrderDetailViewModel : ViewModelBase
    {
        private readonly IOrderService _orderService;
        private readonly IReceiptPrintingService _printingService;
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly RestaurantOrder _order;

        public string TableNumber => _order.TableNumber;
        public string DisplayStatus => _order.Status.ToString().ToUpper();
        public DateTime OrderStartTime => _order.CreatedAt;
        public ObservableCollection<OrderItem> Items { get; }
        public decimal TotalAmount => _order.Total;

        public ICommand PrintReceiptCommand { get; }
        public ICommand SettleOrderCommand { get; }

        public OrderDetailViewModel(
            RestaurantOrder order,
            IOrderService orderService,
            IReceiptPrintingService printingService,
            INotificationService notificationService,
            Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _order = order;
            _orderService = orderService;
            _printingService = printingService;
            _notificationService = notificationService;
            _facilityContext = facilityContext;

            Items = new ObservableCollection<OrderItem>(_order.Items);

            PrintReceiptCommand = new AsyncRelayCommand(ExecutePrintReceipt);
            SettleOrderCommand = new AsyncRelayCommand(ExecuteSettleOrder);
        }

        private async Task ExecutePrintReceipt()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            await _printingService.PrintRestaurantReceiptAsync(facilityId, _order);
            _notificationService.ShowSuccess("Receipt sent to printer.");
        }

        private async Task ExecuteSettleOrder()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            await _orderService.UpdateOrderStatusAsync(facilityId, _order.Id, OrderStatus.Completed);
            _notificationService.ShowSuccess($"Table {TableNumber} settled and cleared.");
        }
    }
}
