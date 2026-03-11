using System.Collections.Generic;
using Management.Domain.Enums;
using Management.Presentation.ViewModels;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.ViewModels.Members;
using Management.Presentation.ViewModels.PointOfSale;
using Management.Presentation.ViewModels.Registrations;
using Management.Presentation.ViewModels.History;
using Management.Presentation.ViewModels.Finance;
using Management.Presentation.ViewModels.GymHome;
using Management.Presentation.ViewModels.Shop;
using Management.Presentation.ViewModels.Shell;

namespace Management.Presentation.Services.Navigation
{
    public class GymNavigationProvider : IFacilityNavigationProvider
    {
        private readonly INavigationRegistry _registry;
        public FacilityType FacilityType => FacilityType.Gym;

        public GymNavigationProvider(INavigationRegistry registry)
        {
            _registry = registry;
        }

        public IEnumerable<NavigationItemMetadata> GetNavigationItems()
        {
            return _registry.GetItems(FacilityType);
        }
    }
}
