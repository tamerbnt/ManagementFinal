using System.Collections.Generic;
using Management.Domain.Enums;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.ViewModels.PointOfSale;
using Management.Presentation.ViewModels.History;
using Management.Presentation.ViewModels.Shop;
using Management.Presentation.ViewModels.GymHome; // Fallback for now

namespace Management.Presentation.Services.Navigation
{
    public class SalonNavigationProvider : IFacilityNavigationProvider
    {
        private readonly INavigationRegistry _registry;
        public FacilityType FacilityType => FacilityType.Salon;

        public SalonNavigationProvider(INavigationRegistry registry)
        {
            _registry = registry;
        }

        public IEnumerable<NavigationItemMetadata> GetNavigationItems()
        {
            return _registry.GetItems(FacilityType);
        }
    }
}
