using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Management.Domain.Enums;
using Management.Presentation.ViewModels;
using Management.Presentation.Extensions; // Added for ViewModelBase

namespace Management.Presentation.Services.Navigation
{
    /// <summary>
    /// Thread-safe implementation of the navigation registry.
    /// </summary>
    public class NavigationRegistry : INavigationRegistry
    {
        private readonly ConcurrentDictionary<FacilityType, List<NavigationItemMetadata>> _registry = new();
        private readonly ConcurrentDictionary<FacilityType, Type> _homeViewRegistry = new();

        public void Register(FacilityType facilityType, NavigationItemMetadata item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var items = _registry.GetOrAdd(facilityType, _ => new List<NavigationItemMetadata>());
            lock (items)
            {
                // Prevent duplicates based on DisplayName or TargetViewModelType
                if (!items.Any(i => i.DisplayName == item.DisplayName || i.TargetViewModelType == item.TargetViewModelType))
                {
                    items.Add(item);
                }
            }
        }

        public IEnumerable<NavigationItemMetadata> GetItems(FacilityType facilityType)
        {
            if (_registry.TryGetValue(facilityType, out var items))
            {
                lock (items)
                {
                    return items.OrderBy(i => i.Order).ToList();
                }
            }
            return Enumerable.Empty<NavigationItemMetadata>();
        }

        public void RegisterHomeView<TViewModel>(FacilityType facilityType) where TViewModel : ViewModelBase
        {
            _homeViewRegistry[facilityType] = typeof(TViewModel);
        }

        public Type GetHomeViewType(FacilityType facilityType)
        {
            if (_homeViewRegistry.TryGetValue(facilityType, out var type))
            {
                return type;
            }
            // Fallback for safety, though registration should happen at startup
            throw new InvalidOperationException($"No home view registered for facility type: {facilityType}. Ensure registry is populated in App.xaml.cs.");
        }

        public void Clear()
        {
            _registry.Clear();
            _homeViewRegistry.Clear();
        }
    }
}
