using System.Collections.Generic;
using Management.Domain.Enums;
using Management.Presentation.ViewModels;
using Management.Presentation.Extensions; // Added for ViewModelBase

namespace Management.Presentation.Services.Navigation
{
    /// <summary>
    /// A central registry for managing navigation items dynamically across different facility types.
    /// </summary>
    public interface INavigationRegistry
    {
        /// <summary>
        /// Registers a navigation item for a specific facility type.
        /// </summary>
        void Register(FacilityType facilityType, NavigationItemMetadata item);

        /// <summary>
        /// Registers the Home ViewModel type for a specific facility type.
        /// </summary>
        void RegisterHomeView<TViewModel>(FacilityType facilityType) where TViewModel : ViewModelBase;

        /// <summary>
        /// Gets the registered Home ViewModel type for a specific facility type.
        /// </summary>
        System.Type GetHomeViewType(FacilityType facilityType);

        /// <summary>
        /// Gets all registered navigation items for a specific facility type.
        /// </summary>
        IEnumerable<NavigationItemMetadata> GetItems(FacilityType facilityType);
        
        /// <summary>
        /// Clears all registered items (useful for testing or full re-initialization).
        /// </summary>
        void Clear();
    }
}
