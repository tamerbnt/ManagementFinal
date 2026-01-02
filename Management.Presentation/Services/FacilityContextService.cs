using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Management.Domain.Enums;
using Management.Domain.Services;

namespace Management.Presentation.Services
{
    public class FacilityConfig
    {
        public FacilityType InitialFacility { get; set; } = FacilityType.Gym;
    }

    public class FacilityContextService : Management.Domain.Services.IFacilityContextService
    {
        private const string ConfigFileName = "facility-config.json";
        private static readonly Dictionary<FacilityType, Guid> FacilityIds = new()
        {
            { FacilityType.Gym, Guid.Parse("00000000-0000-0000-0000-000000000001") },
            { FacilityType.Restaurant, Guid.Parse("00000000-0000-0000-0000-000000000002") },
            { FacilityType.Salon, Guid.Parse("00000000-0000-0000-0000-000000000003") }
        };

        public FacilityType CurrentFacility { get; private set; }
        public Guid CurrentFacilityId => FacilityIds.GetValueOrDefault(CurrentFacility, Guid.Empty);
        public event Action<FacilityType>? FacilityChanged;

        public void SetFacility(FacilityType type) => SwitchFacility(type);

        public void Initialize()
        {
            try
            {
                if (File.Exists(ConfigFileName))
                {
                    var json = File.ReadAllText(ConfigFileName);
                    var config = JsonSerializer.Deserialize<FacilityConfig>(json);
                    CurrentFacility = config?.InitialFacility ?? FacilityType.Gym;
                }
                else
                {
                    CurrentFacility = FacilityType.Gym;
                }
            }
            catch
            {
                CurrentFacility = FacilityType.Gym;
            }

            SwitchFacility(CurrentFacility);
        }

        public void SwitchFacility(FacilityType type)
        {
            CurrentFacility = type;

            var appResources = System.Windows.Application.Current.Resources;
            
            // 1. Identify and remove existing facility themes (Branding and Terminology)
            var existingThemes = appResources.MergedDictionaries
                .Where(d => d.Source != null && 
                           (d.Source.OriginalString.Contains("Resources/Branding.") || 
                            d.Source.OriginalString.Contains("Resources/Terminology.")))
                .ToList();

            foreach (var theme in existingThemes)
            {
                appResources.MergedDictionaries.Remove(theme);
            }

            // 2. Load and add new dictionaries
            try
            {
                // Add Branding
                string brandingPath = $"Resources/Branding.{type}.xaml";
                appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                { 
                    Source = new Uri(brandingPath, UriKind.Relative) 
                });

                // Add Terminology
                string terminologyPath = $"Resources/Terminology.{type}.xaml";
                appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                { 
                    Source = new Uri(terminologyPath, UriKind.Relative) 
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to load facility resources for {Type}", type);
                
                // Fallback to Gym
                if (type != FacilityType.Gym)
                {
                    try
                    {
                        appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                        { 
                            Source = new Uri("Resources/Branding.Gym.xaml", UriKind.Relative) 
                        });
                        appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                        { 
                            Source = new Uri("Resources/Terminology.Gym.xaml", UriKind.Relative) 
                        });
                    }
                    catch { /* Total failure */ }
                }
            }
            
            FacilityChanged?.Invoke(type);

            // 3. Persist selection
            try
            {
                var config = new FacilityConfig { InitialFacility = type };
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigFileName, json);
            }
            catch { /* Log error in real app */ }
        }
    }
}
