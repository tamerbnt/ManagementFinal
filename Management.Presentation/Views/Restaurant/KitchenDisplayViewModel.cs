using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Management.Domain.Models.Restaurant;
using Management.Presentation.Services; // Added
using Management.Presentation.Services.Restaurant;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Restaurant
{
    public class KitchenDisplayViewModel : ViewModelBase
    {
        private readonly IOrderService _orderService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly DispatcherTimer _timer;

        public ObservableCollection<KitchenTicketViewModel> ActiveKitchenOrders { get; } = new();

        public ICommand MarkReadyCommand { get; }

        public KitchenDisplayViewModel(IOrderService orderService, Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _orderService = orderService;
            _facilityContext = facilityContext;
            _orderService.ActiveOrders.CollectionChanged += (s, e) => SyncKitchenOrders();
            
            MarkReadyCommand = new RelayCommand<KitchenTicketViewModel>(async (ticket) => await MarkAsReady(ticket));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => { foreach (var ticket in ActiveKitchenOrders) ticket.UpdateTime(); };
            _timer.Start();

            SyncKitchenOrders();
        }

        private void SyncKitchenOrders()
        {
            // Only show In Progress orders
            var inProgress = _orderService.ActiveOrders.Where(o => o.Status == OrderStatus.InProgress).ToList();
            
            // Add new ones
            foreach (var order in inProgress)
            {
                if (!ActiveKitchenOrders.Any(t => t.OrderId == order.Id))
                {
                    ActiveKitchenOrders.Add(new KitchenTicketViewModel(order));
                    PlayChime();
                }
            }

            // Remove non-pending/in-progress
            var toRemove = ActiveKitchenOrders.Where(t => !inProgress.Any(o => o.Id == t.OrderId)).ToList();
            foreach (var ticket in toRemove) ActiveKitchenOrders.Remove(ticket);
        }

        private async Task MarkAsReady(KitchenTicketViewModel ticket)
        {
            await _orderService.UpdateOrderStatusAsync(_facilityContext.CurrentFacilityId, ticket.OrderId, OrderStatus.Ready);
        }

        private void PlayChime()
        {
            // Simulate 440Hz chime logic
            System.Console.Beep(440, 300);
        }
    }

    public class KitchenTicketViewModel : ViewModelBase
    {
        private readonly RestaurantOrder _order;
        public Guid OrderId => _order.Id;
        public string TableNumber => _order.TableNumber;
        public IEnumerable<OrderItem> Items => _order.Items;

        private TimeSpan _elapsed;
        public TimeSpan ElapsedTimeSpan => _elapsed;

        private string _elapsedTime = string.Empty;
        public string ElapsedTime
        {
            get => _elapsedTime;
            set => SetProperty(ref _elapsedTime, value);
        }

        public KitchenTicketViewModel(RestaurantOrder order)
        {
            _order = order;
            UpdateTime();
        }

        public void UpdateTime()
        {
            _elapsed = DateTime.Now - _order.CreatedAt;
            ElapsedTime = $"{_elapsed.Minutes:D2}:{_elapsed.Seconds:D2}";
            OnPropertyChanged(nameof(ElapsedTimeSpan));
            
            // V2: Change color if > 20 mins
        }
    }
}
