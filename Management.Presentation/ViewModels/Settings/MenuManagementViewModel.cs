using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.ViewModels.Restaurant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Management.Presentation.Extensions;
using Management.Presentation.Services.State;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces.ViewModels;
using Management.Presentation.Helpers;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class MenuManagementViewModel : FacilityAwareViewModelBase, INavigationalLifecycle, IParameterReceiver
    {
        public async void SetParameter(object parameter)
        {
            if (parameter is string param)
            {
                var parts = param.Split('|');
                
                if (parts[0] == "Inventory" && parts.Length > 1 && Guid.TryParse(parts[1], out Guid resourceId))
                {
                    IsInventoryVisible = true;
                    
                    // Ensure inventory data is loaded
                    if (_inventoryViewModel.Resources.Count == 0 && !_inventoryViewModel.IsLoading)
                    {
                        await _inventoryViewModel.InitializeAsync();
                    }

                    var resource = _inventoryViewModel.Resources.FirstOrDefault(r => r.ResourceId == resourceId);
                    if (resource != null)
                    {
                        _inventoryViewModel.SelectedItem = resource; // opens the detail drawer in InventoryView
                    }
                }
                else if (Guid.TryParse(param, out Guid itemId))
                {
                    IsInventoryVisible = false;

                    // Ensure menu data is loaded
                    if (Items.Count == 0 && !IsLoading)
                    {
                        await LoadDataAsync();
                    }

                    var item = Items.FirstOrDefault(i => i.Id == itemId);
                    if (item != null)
                    {
                        SelectedItem = item; // This will trigger OnSelectedItemChanged and open the editor
                    }
                }
            }
        }

        private readonly IMenuService _menuService;
        private readonly ILogger<MenuManagementViewModel> _logger;
        private readonly InventoryViewModel _inventoryViewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISyncService _syncService;

        [ObservableProperty] private ObservableRangeCollection<MenuItemViewModel> _items = new();
        [ObservableProperty] private MenuItemViewModel? _selectedItem;
        [ObservableProperty] private ObservableRangeCollection<string> _categories = new();
        [ObservableProperty] private string? _selectedCategoryFilter;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string _searchText = string.Empty;

        // Drawer state
        [ObservableProperty] private bool _isDrawerOpen;
        [ObservableProperty] private object? _currentDrawerContent;

        // Inventory sub-screen
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(ToggleViewText))]
        [NotifyPropertyChangedFor(nameof(ToggleViewIcon))]
        private bool _isInventoryVisible;
        
        public InventoryViewModel InventoryVM => _inventoryViewModel;

        public MenuManagementViewModel(
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IMenuService menuService,
            IFacilityContextService facilityContext,
            ILogger<MenuManagementViewModel> logger,
            InventoryViewModel inventoryViewModel,
            IServiceProvider serviceProvider,
            ISyncService syncService) : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _menuService = menuService;
            _logger = logger;
            _inventoryViewModel = inventoryViewModel;
            _serviceProvider = serviceProvider;
            _syncService = syncService;

            _syncService.SyncCompleted += OnSyncCompleted;
            
            // Subscribe to back navigation from inventory
            _inventoryViewModel.NavigatedBack += (_, _) => IsInventoryVisible = false;

            LoadDataCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(LoadDataAsync);
            AddCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(AddNewItem);
            DeleteCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(DeleteAsync, () => SelectedItem != null);
            ToggleViewCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ToggleViewAsync);
            CloseDrawerCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CloseDrawer);
            SelectedItemCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<MenuItemViewModel>(item => SelectedItem = item);

            // Force Menu view as default landing
            IsInventoryVisible = false;
        }

        public IAsyncRelayCommand LoadDataCommand { get; }
        public ICommand AddCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }
        public IAsyncRelayCommand ToggleViewCommand { get; }
        public ICommand CloseDrawerCommand { get; }
        public ICommand SelectedItemCommand { get; }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task PreInitializeAsync()
        {
            Title = GetTerm("Strings.Settings.MenuManagement");
            return Task.CompletedTask;
        }

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            if (LoadDataCommand.CanExecute(null))
            {
                await LoadDataCommand.ExecuteAsync(null);
            }
        }

        public override void ResetState()
        {
            base.ResetState();
            Items.Clear();
            Categories.Clear();
            SelectedItem = null;
            SelectedCategoryFilter = null;
            StatusMessage = string.Empty;
            SearchText = string.Empty;
            IsDrawerOpen = false;
            CurrentDrawerContent = null;
            IsInventoryVisible = false;
        }

        public string ToggleViewText => IsInventoryVisible ? GetTerm("Strings.Settings.MenuManagement") : GetTerm("Strings.Restaurant.Inventory");
        public string ToggleViewIcon => IsInventoryVisible ? "IconShop" : "IconDashboard";

        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            Title = GetTerm("Strings.Settings.MenuManagement");
            OnPropertyChanged(nameof(ToggleViewText));
        }


        private async Task ToggleViewAsync()
        {
            IsInventoryVisible = !IsInventoryVisible;
            if (IsInventoryVisible)
            {
                await _inventoryViewModel.InitializeAsync();
            }
        }

        public IEnumerable<MenuItemViewModel> FilteredItems 
        {
            get
            {
                var filtered = Items.AsEnumerable();
                
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                                                 i.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter) && SelectedCategoryFilter != "All Categories")
                {
                    filtered = filtered.Where(i => i.Category == SelectedCategoryFilter);
                }

                return filtered;
            }
        }

        partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredItems));
        partial void OnSelectedCategoryFilterChanged(string? value) => OnPropertyChanged(nameof(FilteredItems));

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading menu items...";
            try
            {
                var dtos = await _menuService.GetMenuItemsAsync(_facilityContext.CurrentFacilityId);
                Items.ReplaceRange(dtos.Select(d => new MenuItemViewModel(d)));
                
                // Update categories list
                var cats = Items.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();
                var newCategories = new List<string> { "All Categories" };
                newCategories.AddRange(cats);

                Categories.ReplaceRange(newCategories);
                SelectedCategoryFilter = "All Categories";

                StatusMessage = string.Empty;
                OnPropertyChanged(nameof(FilteredItems));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load menu items");
                StatusMessage = "Failed to load menu items.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedItemChanged(MenuItemViewModel? value)
        {
            // Update isActive state for all items to handle highlighting
            foreach (var item in Items)
            {
                item.IsActive = (item == value);
            }

            DeleteCommand.NotifyCanExecuteChanged();
            if (value != null)
            {
                OpenEditor(value);
            }
        }

        private void OpenEditor(MenuItemViewModel? item = null)
        {
            CleanupEditor();

            var editorVm = _serviceProvider.GetRequiredService<MenuItemEditorViewModel>();
            editorVm.Initialize(item);
            
            editorVm.Saved += OnMenuItemEditorSaved;
            editorVm.Canceled += OnEditorCanceled;

            CurrentDrawerContent = editorVm;
            IsDrawerOpen = true;
        }

        private async void OnMenuItemEditorSaved(object? sender, EventArgs e)
        {
            IsDrawerOpen = false;
            if (SelectedItem != null) SelectedItem.IsActive = false;
            CleanupEditor();
            await LoadDataAsync();
        }

        private void OnEditorCanceled(object? sender, EventArgs e)
        {
            IsDrawerOpen = false;
            if (SelectedItem != null) SelectedItem.IsActive = false;
            CleanupEditor();
        }

        private void CleanupEditor()
        {
            if (CurrentDrawerContent is MenuItemEditorViewModel menuEditor)
            {
                menuEditor.Saved -= OnMenuItemEditorSaved;
                menuEditor.Canceled -= OnEditorCanceled;
            }
            CurrentDrawerContent = null;
        }

        private void CloseDrawer()
        {
            IsDrawerOpen = false;
            if (SelectedItem != null) SelectedItem.IsActive = false;
            CleanupEditor();
        }

        private void AddNewItem()
        {
            OpenEditor(null);
        }

        private async Task DeleteAsync()
        {
            if (SelectedItem == null || SelectedItem.Id == Guid.Empty) return;

            IsLoading = true;
            StatusMessage = "Deleting item...";
            try
            {
                var success = await _menuService.DeleteMenuItemAsync(SelectedItem.Id);
                if (success)
                {
                    Items.Remove(SelectedItem);
                    SelectedItem = Items.FirstOrDefault();
                    StatusMessage = "Item deleted successfully.";
                }
                else
                {
                    StatusMessage = "Failed to delete item.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting menu item");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (!ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[MenuManagement] Sync debounce passed, refreshing menu data...");
                await LoadDataAsync();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }
            }
            base.Dispose(disposing);
        }
    }

    public partial class MenuItemViewModel : ObservableObject
    {
        private readonly RestaurantMenuItemDto _dto;

        public Guid Id => _dto.Id;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _category;

        [ObservableProperty]
        private decimal _price;

        [ObservableProperty]
        private bool _isAvailable;

        [ObservableProperty]
        private bool _isActive;

        public MenuItemViewModel(RestaurantMenuItemDto dto)
        {
            _dto = dto;
            _name = dto.Name;
            _category = dto.Category;
            _price = dto.Price;
            _isAvailable = dto.IsAvailable;
        }

        public void UpdateFromDto(RestaurantMenuItemDto dto)
        {
            Name = dto.Name;
            Category = dto.Category;
            Price = dto.Price;
            IsAvailable = dto.IsAvailable;
        }

        public RestaurantMenuItemDto ToDto()
        {
            return new RestaurantMenuItemDto
            {
                Id = _dto.Id,
                Name = Name,
                Category = Category,
                Price = Price,
                IsAvailable = IsAvailable,
                ImagePath = _dto.ImagePath,
                Ingredients = _dto.Ingredients,
                FacilityId = _dto.FacilityId,
                TenantId = _dto.TenantId
            };
        }
    }
}
