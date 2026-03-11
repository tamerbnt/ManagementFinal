using CommunityToolkit.Mvvm.ComponentModel;
using Management.Application.DTOs;

namespace Management.Presentation.ViewModels.Restaurant
{
    /// <summary>
    /// Wraps a RestaurantMenuItemDto with an observable IsInCurrentOrder flag,
    /// mirroring the IsActive pattern used in MenuManagementView for clean DataTrigger bindings.
    /// </summary>
    public partial class SelectableMenuItemViewModel : ObservableObject
    {
        public RestaurantMenuItemDto Item { get; }

        // Forwarded properties for XAML bindings
        public string Name     => Item.Name;
        public string Category => Item.Category;
        public decimal Price   => Item.Price;

        [ObservableProperty]
        private bool _isInCurrentOrder;

        public SelectableMenuItemViewModel(RestaurantMenuItemDto item)
        {
            Item = item;
        }
    }
}
