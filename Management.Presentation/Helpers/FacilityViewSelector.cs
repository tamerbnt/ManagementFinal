using System;
using System.Windows;
using System.Windows.Controls;
using Management.Domain.Enums;
using Management.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.Helpers
{
    /// <summary>
    /// Selects a DataTemplate based on the current active facility.
    /// Allows shared ViewModels to resolve to facility-specific specialized Views.
    /// </summary>
    public class FacilityViewSelector : DataTemplateSelector
    {
        public Type? GymViewType { get; set; }
        public Type? SalonViewType { get; set; }
        public Type? RestaurantViewType { get; set; }
        public Type? DefaultViewType { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null) return null!;

            var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
            if (serviceProvider == null) return null!;

            var facilityService = serviceProvider.GetRequiredService<IFacilityContextService>();
            
            Type? targetViewType = facilityService.CurrentFacility switch
            {
                FacilityType.Gym => GymViewType ?? DefaultViewType,
                FacilityType.Salon => SalonViewType ?? DefaultViewType,
                FacilityType.Restaurant => RestaurantViewType ?? DefaultViewType,
                _ => DefaultViewType
            };

            if (targetViewType == null) return null!;

            return CreateDataTemplate(targetViewType);
        }

        private DataTemplate CreateDataTemplate(Type viewType)
        {
            try
            {
                var factory = new FrameworkElementFactory(viewType);
                return new DataTemplate { VisualTree = factory };
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create DataTemplate for {ViewType}", viewType.Name);
                return null!;
            }
        }
    }
}
