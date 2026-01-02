using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Models;
using Management.Domain.Models.Salon;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Restaurant;
using Management.Presentation.Services.Salon;

namespace Management.Presentation.Views.Salon
{
    public class CompletionViewModel : ViewModelBase
    {
        private readonly ISalonService _salonService;
        private readonly IProductService _productService;
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;
        private readonly IReceiptPrintingService _receiptService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;

        private readonly Appointment _appointment;

        public string ClientName => _appointment.ClientName;
        public string ServiceName => _appointment.ServiceName;
        public decimal BasePrice { get; }

        public ObservableCollection<ProductUsageViewModel> UsedProducts { get; } = new();
        public ObservableCollection<ProductDto> AvailableProducts { get; } = new();

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value);
        }

        public ICommand AddProductCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand CancelCommand { get; }

        public CompletionViewModel(
            Appointment appointment,
            ISalonService salonService,
            IProductService productService,
            IModalNavigationService modalService,
            INotificationService notificationService,
            IReceiptPrintingService receiptService,
            Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _appointment = appointment;
            _salonService = salonService;
            _productService = productService;
            _modalService = modalService;
            _notificationService = notificationService;
            _receiptService = receiptService;
            _facilityContext = facilityContext;

            var service = _salonService.Services.First(s => s.Id == _appointment.ServiceId);
            BasePrice = service.BasePrice;

            AddProductCommand = new RelayCommand<ProductDto>(ExecuteAddProduct);
            CompleteCommand = new RelayCommand(async () => await ExecuteComplete());
            CancelCommand = new RelayCommand(() => _modalService.CloseModal());

            LoadProducts();
            CalculateTotal();
        }

        private async void LoadProducts()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var result = await _productService.GetActiveProductsAsync(facilityId);
            if (result.IsSuccess)
            {
                foreach (var p in result.Value.Take(10)) AvailableProducts.Add(p);
            }
        }

        private void ExecuteAddProduct(ProductDto product)
        {
            if (product == null) return;
            var existing = UsedProducts.FirstOrDefault(u => u.ProductId == product.Id);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                UsedProducts.Add(new ProductUsageViewModel(product, () => CalculateTotal()));
            }
            CalculateTotal();
        }

        private void CalculateTotal()
        {
            TotalAmount = BasePrice + UsedProducts.Sum(p => p.Total);
        }

        private async Task ExecuteComplete()
        {
            var usage = UsedProducts.Select(u => new ProductUsage
            {
                ProductId = u.ProductId,
                ProductName = u.Name,
                Quantity = u.Quantity,
                PricePerUnit = u.Price
            });

            await _salonService.CompleteAppointmentAsync(_appointment.Id, usage);
            await _receiptService.PrintSalonReceiptAsync(Guid.Empty, _appointment, TotalAmount);
            
            _notificationService.ShowNotification("Appointment completed and stock deducted", NotificationType.Success);
            _modalService.CloseModal();
        }
    }

    public class ProductUsageViewModel : ViewModelBase
    {
        private int _quantity = 1;
        private Action _onChanged;

        public ProductUsageViewModel(ProductDto p, Action onChanged)
        {
            ProductId = p.Id;
            Name = p.Name;
            Price = p.Price;
            _onChanged = onChanged;
        }

        public Guid ProductId { get; }
        public string Name { get; }
        public decimal Price { get; }
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(Total));
                    _onChanged?.Invoke();
                }
            }
        }
        public decimal Total => Quantity * Price;
    }
}
