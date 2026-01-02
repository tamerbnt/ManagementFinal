using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Management.Domain.Models.Restaurant;
using Management.Presentation.Services;
using Management.Presentation.Services.Restaurant;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Restaurant
{
    public class OrderViewModel : ViewModelBase
    {
        private readonly IOrderService _orderService;
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly IUndoService _undoService;
        
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set { if (SetProperty(ref _searchQuery, value)) OnPropertyChanged(nameof(FilteredMenuItems)); }
        }

        public RestaurantOrder Order { get; }
        
        public ObservableCollection<string> Categories { get; } = new() { "All", "Appetizers", "Mains", "Drinks", "Desserts" };
        
        private string _selectedCategory = "All";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetProperty(ref _selectedCategory, value)) OnPropertyChanged(nameof(FilteredMenuItems)); }
        }

        public ObservableCollection<RestaurantMenuItem> MenuItems { get; } = new();
        public IEnumerable<RestaurantMenuItem> FilteredMenuItems => 
            MenuItems.Where(i => (SelectedCategory == "All" || i.Category == SelectedCategory) &&
                                 (string.IsNullOrWhiteSpace(SearchQuery) || 
                                  i.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                  i.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));

        public ICommand AddItemCommand { get; }
        public ICommand IncrementItemCommand { get; }
        public ICommand DecrementItemCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand SendOrderCommand { get; }

        public OrderViewModel(
            IOrderService orderService, 
            INotificationService notificationService, 
            Management.Domain.Services.IFacilityContextService facilityContext,
            IUndoService undoService,
            string tableNumber)
        {
            _orderService = orderService;
            _notificationService = notificationService;
            _facilityContext = facilityContext;
            _undoService = undoService;
            Order = new RestaurantOrder { TableNumber = tableNumber, Status = OrderStatus.Pending };
            
            AddItemCommand = new RelayCommand<RestaurantMenuItem>(async (item) => await AddItem(item));
            IncrementItemCommand = new RelayCommand<OrderItem>((item) => { item.Quantity++; Recalculate(); });
            DecrementItemCommand = new RelayCommand<OrderItem>(async (item) => await DecrementItem(item));
            RemoveItemCommand = new RelayCommand<OrderItem>(async (item) => await RemoveWithUndo(item));
            SendOrderCommand = new RelayCommand(async () => await SendOrder());
            
            LoadMockMenu();
        }

        private async Task RemoveWithUndo(OrderItem item)
        {
            var index = Order.Items.IndexOf(item);
            if (index < 0) return;

            Order.Items.Remove(item);
            Recalculate();

            _undoService.Push(
                $"Removed {item.Name} from Table {Order.TableNumber}",
                async () => {
                    Order.Items.Insert(index, item);
                    Recalculate();
                });
        }

        private async Task AddItem(RestaurantMenuItem item)
        {
            var existing = Order.Items.FirstOrDefault(i => i.Name == item.Name);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                Order.Items.Add(new OrderItem { Name = item.Name, Price = item.Price, Quantity = 1 });
            }
            Recalculate();
        }

        private async Task SendOrder()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            Order.Status = OrderStatus.InProgress;
            await _orderService.UpdateOrderStatusAsync(facilityId, Order.Id, OrderStatus.InProgress);
            _notificationService.ShowSuccess("Order sent to kitchen.");
        }

        private async Task DecrementItem(OrderItem item)
        {
            if (item.Quantity > 1)
            {
                item.Quantity--;
                Recalculate();
            }
            else
            {
                await RemoveWithUndo(item);
            }
        }

        private void Recalculate()
        {
            Order.Subtotal = Order.Items.Sum(i => i.Total);
            Order.Tax = Order.Subtotal * 0.15m;
            OnPropertyChanged(nameof(Order));
        }

        private void LoadMockMenu()
        {
            MenuItems.Add(new RestaurantMenuItem { Name = "Bruschetta", Category = "Appetizers", Price = 8.50m });
            MenuItems.Add(new RestaurantMenuItem { Name = "Calamari", Category = "Appetizers", Price = 12.00m });
            MenuItems.Add(new RestaurantMenuItem { Name = "Steak Frites", Category = "Mains", Price = 28.00m });
            MenuItems.Add(new RestaurantMenuItem { Name = "Salmon", Category = "Mains", Price = 24.50m });
            MenuItems.Add(new RestaurantMenuItem { Name = "Espresso", Category = "Drinks", Price = 3.50m });
            MenuItems.Add(new RestaurantMenuItem { Name = "Cheesecake", Category = "Desserts", Price = 9.00m });
        }
    }
}
