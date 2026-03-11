using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Models.Restaurant;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Presentation.Services.State;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Messages;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class RestaurantOrderingViewModel : FacilityAwareViewModelBase, IParameterReceiver
    {
        private readonly IMenuService _menuService;
        private readonly IOrderService _orderService;
        private readonly INavigationService _navigationService;
        private readonly SessionManager _sessionManager;
        private readonly SemaphoreSlim _orderUpdateLock = new(1, 1);

        private Guid _initialOrderId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmptyState))]
        private ObservableCollection<TakeoutSessionViewModel> _openSessions = new();

        public bool IsEmptyState => !OpenSessions.Any();

        [ObservableProperty]
        private TakeoutSessionViewModel? _currentSession;

        [ObservableProperty]
        private ObservableCollection<SelectableMenuItemViewModel> _filteredItems = new();

        private List<RestaurantMenuItemDto> _allItems = new();

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private string _selectedCategory = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public bool CanAddSession => OpenSessions.Count < 4;

        public RestaurantOrderingViewModel(
            IMenuService menuService,
            IOrderService orderService,
            INavigationService navigationService,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<RestaurantOrderingViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            SessionManager sessionManager,
            ILocalizationService? localizationService = null)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

            Title = GetTerm("Terminology.Restaurant.Order.MenuTitle");
            _selectedCategory = GetTerm("Terminology.Restaurant.Order.AllCategories");
        }

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Terminology.Restaurant.Order.MenuTitle");
            SelectedCategory = GetTerm("Terminology.Restaurant.Order.AllCategories");
        }

        public void SetParameter(object parameter)
        {
            if (parameter is Guid orderId)
            {
                _initialOrderId = orderId;
                _ = Task.Run(async () => await InitializeAsync());
            }
        }

        private async Task InitializeAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                // Load menu
                var menuItems = await _menuService.GetMenuItemsAsync(CurrentFacilityId);
                _allItems = menuItems.ToList();

                // Build categories
                var cats = new List<string> { GetTerm("Terminology.Restaurant.Order.AllCategories") };
                cats.AddRange(_allItems.Select(i => i.Category).Distinct().OrderBy(c => c));
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Categories = new ObservableCollection<string>(cats);
                });

                // Load all existing active takeout orders to restore sessions
                var activeResult = await _orderService.GetActiveOrdersAsync(CurrentFacilityId);
                if (activeResult.IsSuccess)
                {
                    // Filter to only takeout (no TableId) and sort by creation
                    var activeTakeout = activeResult.Value
                        .Where(o => o.TableId == null)
                        .OrderBy(o => o.CreatedAt);

                    foreach (var order in activeTakeout)
                    {
                        // Avoid adding the same order twice if it's the initial one
                        if (order.Id != _initialOrderId)
                        {
                            await AddSessionInternalAsync(order.Id);
                        }
                    }
                }

                // Add or select the initial session
                if (_initialOrderId != Guid.Empty)
                {
                    var existing = OpenSessions.FirstOrDefault(s => s.OrderId == _initialOrderId);
                    if (existing != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => CurrentSession = existing);
                    }
                    else
                    {
                        await AddSessionInternalAsync(_initialOrderId);
                    }
                }
                else if (OpenSessions.Any())
                {
                    // If no specific order was requested, select the first available open session
                    System.Windows.Application.Current.Dispatcher.Invoke(() => CurrentSession = OpenSessions.FirstOrDefault());
                }
                // REMOVED: the auto-create logic if initial is empty and there are no sessions.

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsEmptyState));
                });

                FilterItems();
            });
        }

        [RelayCommand(CanExecute = nameof(CanAddSession))]
        private async Task AddSessionAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                _logger.LogInformation("Creating new takeout session...");
                var result = await _orderService.StartOrderAsync(
                    null,
                    null,
                    _sessionManager.CurrentTenantId,
                    CurrentFacilityId);

                if (result.IsSuccess)
                {
                    await AddSessionInternalAsync(result.Value);
                    _toastService?.ShowSuccess(GetTerm("Terminology.Restaurant.Order.Toast.NewOrderCreated") ?? "New order session created");
                }
                else
                {
                    _toastService?.ShowError(result.Error.Message);
                }
                
                OnPropertyChanged(nameof(CanAddSession));
                AddSessionCommand.NotifyCanExecuteChanged();
            });
        }

        private async Task AddSessionInternalAsync(Guid orderId)
        {
            // Initial label will be updated once CurrentOrder is loaded in TakeoutSessionViewModel
            var session = new TakeoutSessionViewModel(orderId, GetTerm("Terminology.Restaurant.Order.Ticket").Replace("{0}", "..."), _terminologyService);
            var orderResult = await _orderService.GetOrderByIdAsync(orderId);
            if (orderResult.IsSuccess)
            {
                session.CurrentOrder = orderResult.Value;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                OpenSessions.Add(session);
                if (CurrentSession == null || orderId == _initialOrderId)
                {
                    CurrentSession = session;
                }
                AddSessionCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsEmptyState));
            });
        }

        [RelayCommand]
        private async Task CloseSessionAsync(TakeoutSessionViewModel session)
        {
            if (session == null) return;

            await ExecuteSafeAsync(async () =>
            {
                // Check if the order is completely empty (no items) before closing/cancelling
                bool wasEmpty = session.CurrentOrder == null || !session.CurrentOrder.Items.Any();

                // Remove from local list
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OpenSessions.Remove(session);
                });

                // If the ticket was left completely empty when closed, aggressively cancel it
                // to prevent ghost records from clogging up the DB
                if (wasEmpty)
                {
                    _logger.LogInformation("Reaping empty parked order {OrderId}", session.OrderId);
                    await _orderService.CancelOrderAsync(session.OrderId);
                }

                // Update selection
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (CurrentSession == session)
                    {
                        CurrentSession = OpenSessions.LastOrDefault();
                    }
                    AddSessionCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(IsEmptyState));
                });
            });
        }


        [RelayCommand]
        private void SelectCategory(string category)
        {
            SelectedCategory = category;
            FilterItems();
        }

        partial void OnSearchTextChanged(string value) => FilterItems();

        partial void OnCurrentSessionChanged(TakeoutSessionViewModel? value)
        {
            UpdateSelectionStates();
        }

        private void FilterItems()
        {
            var filtered = _allItems.AsEnumerable();

            if (SelectedCategory != GetTerm("Terminology.Restaurant.Order.AllCategories"))
            {
                filtered = filtered.Where(i => i.Category == SelectedCategory);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                FilteredItems = new ObservableCollection<SelectableMenuItemViewModel>(
                    filtered.Select(dto => new SelectableMenuItemViewModel(dto)));
                UpdateSelectionStates();
            });
        }

        private void UpdateSelectionStates()
        {
            if (FilteredItems == null) return;
            var orderedNames = CurrentSession?.CurrentOrder?.Items
                .Select(i => i.Name).ToHashSet() ?? new HashSet<string>();
            foreach (var item in FilteredItems)
                item.IsInCurrentOrder = orderedNames.Contains(item.Name);
        }

        [RelayCommand]
        private async Task AddToOrderAsync(SelectableMenuItemViewModel selectable)
        {
            if (CurrentSession == null || selectable == null) return;
            var item = selectable.Item;

            await _orderUpdateLock.WaitAsync();
            try
            {
                await ExecuteSafeAsync(async () =>
                {
                    // TOGGLE LOGIC: If item exists in order, remove it. Otherwise add it.
                    var existingItem = CurrentSession.CurrentOrder?.Items.FirstOrDefault(i => i.Name == item.Name);

                    if (existingItem != null)
                    {
                        var result = await _orderService.RemoveItemFromOrderAsync(CurrentSession.OrderId, item.Name);
                        if (result.IsSuccess)
                        {
                            await RefreshCurrentOrderAsync();
                            // _toastService?.ShowSuccess($"Removed {item.Name}");
                        }
                        else
                        {
                            _toastService?.ShowError(result.Error.Message);
                        }
                    }
                    else
                    {
                        var result = await _orderService.AddItemToOrderAsync(CurrentSession.OrderId, item.Name, item.Price, 1);
                        if (result.IsSuccess)
                        {
                            await RefreshCurrentOrderAsync();
                            // _toastService?.ShowSuccess(string.Format(GetTerm("Terminology.Restaurant.Order.Toast.Added"), item.Name));
                        }
                        else
                        {
                            _toastService?.ShowError(result.Error.Message);
                        }
                    }
                });
            }
            finally
            {
                _orderUpdateLock.Release();
            }
        }

        [RelayCommand]
        private async Task AddMultipleToOrderAsync(RestaurantMenuItemDto item)
        {
            if (CurrentSession == null) return;

            // This would typically open a small popup or just add a default batch
            // For now, let's implement a simple "Add 5" or similar as a placeholder for the logic
            await ExecuteSafeAsync(async () =>
            {
                var result = await _orderService.AddItemToOrderAsync(CurrentSession.OrderId, item.Name, item.Price, 5);
                if (result.IsSuccess)
                {
                    await RefreshCurrentOrderAsync();
                    // _toastService?.ShowSuccess($"Added 5x {item.Name}");
                }
            });
        }

        [RelayCommand]
        private async Task PrintOrderAsync()
        {
            if (CurrentSession == null || CurrentSession.CurrentOrder == null) return;

            await ExecuteSafeAsync(async () =>
            {
                _toastService?.ShowInfo(GetTerm("Terminology.Restaurant.Order.Toast.Printing") ?? "Printing Tickets (Staff & Client)...");
                
                var result = await _orderService.PrintOrderAsync(CurrentSession.OrderId);
                
                if (result.IsSuccess)
                {
                    _toastService?.ShowSuccess(GetTerm("Terminology.Restaurant.Order.Toast.PrintSuccess") ?? "Order Tickets Printed Successfully");
                }
                else
                {
                    _toastService?.ShowError(result.Error.Message);
                }
            });
        }

        [RelayCommand]
        private async Task IncreaseQuantityAsync(OrderItemDto item)
        {
            if (CurrentSession == null) return;

            await _orderUpdateLock.WaitAsync();
            try
            {
                await ExecuteSafeAsync(async () =>
                {
                    var result = await _orderService.UpdateItemQuantityAsync(CurrentSession.OrderId, item.Name, item.Quantity + 1);
                    if (result.IsSuccess) await RefreshCurrentOrderAsync();
                });
            }
            finally
            {
                _orderUpdateLock.Release();
            }
        }

        [RelayCommand]
        private async Task DecreaseQuantityAsync(OrderItemDto item)
        {
            if (CurrentSession == null) return;

            await _orderUpdateLock.WaitAsync();
            try
            {
                await ExecuteSafeAsync(async () =>
                {
                    var result = await _orderService.UpdateItemQuantityAsync(CurrentSession.OrderId, item.Name, item.Quantity - 1);
                    if (result.IsSuccess) await RefreshCurrentOrderAsync();
                });
            }
            finally
            {
                _orderUpdateLock.Release();
            }
        }

        [RelayCommand]
        private async Task SendToKitchenAsync()
        {
            if (CurrentSession == null) return;

            await ExecuteSafeAsync(async () =>
            {
                var result = await _orderService.SendToKitchenAsync(CurrentSession.OrderId);
                if (result.IsSuccess)
                {
                    _toastService?.ShowSuccess(GetTerm("Terminology.Restaurant.Order.SendToKitchen"));
                    
                    var sessionToRemove = CurrentSession;
                    if (OpenSessions.Count > 1)
                    {
                        OpenSessions.Remove(sessionToRemove);
                        CurrentSession = OpenSessions.LastOrDefault();
                        OnPropertyChanged(nameof(CanAddSession));
                        AddSessionCommand.NotifyCanExecuteChanged();
                    }
                    else
                    {
                        await _navigationService.NavigateToHomeAsync();
                    }
                    WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(CurrentFacilityId));
                }
            });
        }

        [RelayCommand]
        private async Task PayAndCompleteAsync()
        {
            if (CurrentSession == null || CurrentSession.CurrentOrder == null) return;

            await ExecuteSafeAsync(async () =>
            {
                var result = await _orderService.CompleteOrderAsync(CurrentSession.OrderId);
                if (result.IsSuccess)
                {
                    _toastService?.ShowSuccess(string.Format(GetTerm("Terminology.Restaurant.Order.Toast.Paid"), CurrentSession.DisplayName));
                    
                    var sessionToRemove = CurrentSession;
                    if (OpenSessions.Count > 1)
                    {
                        OpenSessions.Remove(sessionToRemove);
                        CurrentSession = OpenSessions.LastOrDefault();
                        OnPropertyChanged(nameof(CanAddSession));
                        AddSessionCommand.NotifyCanExecuteChanged();
                    }
                    else
                    {
                        await _navigationService.NavigateToHomeAsync();
                    }
                    WeakReferenceMessenger.Default.Send(new TableStatusChangedMessage(CurrentFacilityId));
                }
                else
                {
                    ShowError(result.Error.Message);
                }
            });
        }

        [RelayCommand]
        private async Task CancelOrderAsync()
        {
            if (CurrentSession == null) return;
            await CancelOrderInternalAsync(CurrentSession.OrderId);
        }

        private async Task CancelOrderInternalAsync(Guid orderId)
        {
            await ExecuteSafeAsync(async () =>
            {
                var result = await _orderService.CancelOrderAsync(orderId);
                if (result.IsSuccess)
                {
                    var session = OpenSessions.FirstOrDefault(s => s.OrderId == orderId);
                    if (session != null)
                    {
                        OpenSessions.Remove(session);
                        if (OpenSessions.Count == 0)
                        {
                            await _navigationService.NavigateToHomeAsync();
                        }
                        else
                        {
                            CurrentSession = OpenSessions.LastOrDefault();
                            OnPropertyChanged(nameof(CanAddSession));
                        }
                    }
                }
            });
        }

        private async Task RefreshCurrentOrderAsync()
        {
            if (CurrentSession == null) return;

            var result = await _orderService.GetOrderByIdAsync(CurrentSession.OrderId);
            if (result.IsSuccess)
            {
                // All three mutations must run on the UI thread so WPF binding
                // notifications (PropertyChanged) are processed correctly.
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentSession.CurrentOrder = result.Value;
                    CurrentSession.RefreshTotals();
                    UpdateSelectionStates();
                });
            }
        }
    }
}

