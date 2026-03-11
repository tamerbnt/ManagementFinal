using System.Collections.Generic;
using Management.Domain.Enums;

namespace Management.Presentation.Services.Navigation
{
    /// <summary>
    /// Strategy interface for providing facility-specific navigation items.
    /// </summary>
    public interface IFacilityNavigationProvider
    {
        /// <summary>
        /// The facility type this provider handles.
        /// </summary>
        FacilityType FacilityType { get; }

        /// <summary>
        /// Gets the navigation items for the facility.
        /// </summary>
        IEnumerable<NavigationItemMetadata> GetNavigationItems();
    }
}
