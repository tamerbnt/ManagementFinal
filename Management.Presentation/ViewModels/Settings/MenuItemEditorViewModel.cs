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
using Management.Presentation.Services.State;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class MenuItemEditorViewModel : ObservableObject
    {
        private readonly IMenuService _menuService;
        private readonly IFacilityContextService _facilityContext;
        private readonly SessionManager _sessionManager;

        public event EventHandler? Saved;
        public event EventHandler? Canceled;

        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _editingName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _editingCategory = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private decimal _editingPrice;

        [ObservableProperty]
        private bool _editingIsAvailable = true;

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _editorTitle = "Edit Menu Item";

        public MenuItemEditorViewModel(
            IMenuService menuService,
            IFacilityContextService facilityContext,
            SessionManager sessionManager)
        {
            _menuService = menuService;
            _facilityContext = facilityContext;
            _sessionManager = sessionManager;

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(Cancel);
        }

        public IAsyncRelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public void Initialize(MenuItemViewModel? existingItem = null)
        {
            // Standard Restaurant Categories
            Categories = new ObservableCollection<string>
            {
                "Food",
                "Boisson",
                "Dessert",
                "Appetizer",
                "Special"
            };

            if (existingItem == null)
            {
                EditorTitle = "Add New Menu Item";
                Id = Guid.Empty;
                EditingName = string.Empty;
                EditingCategory = Categories.First();
                EditingPrice = 0;
                EditingIsAvailable = true;
            }
            else
            {
                EditorTitle = "Edit Menu Item";
                Id = existingItem.Id;
                EditingName = existingItem.Name;
                EditingCategory = existingItem.Category;
                EditingPrice = existingItem.Price;
                EditingIsAvailable = existingItem.IsAvailable;
            }

            StatusMessage = string.Empty;
            SaveCommand.NotifyCanExecuteChanged();
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(EditingName) && 
                   !string.IsNullOrWhiteSpace(EditingCategory) && 
                   EditingPrice >= 0;
        }

        private async Task SaveAsync()
        {
            if (!CanSave()) return;

            IsLoading = true;
            StatusMessage = "Saving...";

            try
            {
                var dto = new RestaurantMenuItemDto
                {
                    Id = Id,
                    Name = EditingName,
                    Category = EditingCategory,
                    Price = EditingPrice,
                    IsAvailable = EditingIsAvailable,
                    FacilityId = _facilityContext.CurrentFacilityId,
                    TenantId = _sessionManager.CurrentTenantId
                };

                bool success;
                if (Id == Guid.Empty)
                {
                    dto.Id = Guid.NewGuid();
                    success = await _menuService.AddMenuItemAsync(dto);
                }
                else
                {
                    success = await _menuService.UpdateMenuItemAsync(dto);
                }

                if (success)
                {
                    StatusMessage = "Save successful!";
                    Saved?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    StatusMessage = "Failed to save menu item.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                // Log error
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Cancel()
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
    }
}
