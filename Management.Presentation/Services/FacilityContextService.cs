using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Services.Localization;

namespace Management.Presentation.Services
{
    public class FacilityConfig
    {
        public FacilityType InitialFacility { get; set; } = FacilityType.Gym;
        public string LanguageCode { get; set; } = "en";
        public string PublicSlug { get; set; } = string.Empty;
        
        // Removed [JsonConverter(typeof(JsonStringEnumConverter))] as it crashes when applied directly to a Dictionary<Enum, Guid> property
        public Dictionary<FacilityType, Guid>? FacilityIds { get; set; }
    }

    public class FacilityContextService : Management.Domain.Services.IFacilityContextService
    {
        private readonly IDispatcher _dispatcher;
        private readonly ILocalizationService _localizationService;
        private readonly string _configPath;
        
        // Default seed IDs removed (Relying on discovery)
        private readonly Dictionary<FacilityType, Guid> _dynamicFacilityIds = new();

        public FacilityType CurrentFacility { get; private set; }
        public Guid CurrentFacilityId => _dynamicFacilityIds.GetValueOrDefault(CurrentFacility, Guid.Empty);
        public string LanguageCode { get; private set; } = "en";
        public string PublicSlug { get; private set; } = string.Empty;
        public event Action<FacilityType>? FacilityChanged;

        public async void SetFacility(FacilityType type)
        {
            Serilog.Log.Information("[FacilityContext] SetFacility({Type}) called. CurrentFacilityId at this moment: {Id}", type, _dynamicFacilityIds.GetValueOrDefault(type, Guid.Empty));
            await SwitchFacility(type);
        }

        public void SaveLanguage(string languageCode)
        {
            LanguageCode = languageCode;
            Serilog.Log.Information("[FacilityContext] Language preference saved: {Lang}", languageCode);
            SaveConfig();
        }

        public void UpdateFacilities(Dictionary<FacilityType, Guid> facilityMappings)
        {
            if (facilityMappings == null || !facilityMappings.Any())
            {
                Serilog.Log.Warning("[FacilityContext] CRITICAL: Attempted to update facilities with an empty list. Rejecting to protect local cache.");
                return;
            }

            foreach (var mapping in facilityMappings)
            {
                _dynamicFacilityIds[mapping.Key] = mapping.Value;
                Serilog.Log.Information($"[FacilityContext] Updated {mapping.Key} to {mapping.Value}");
            }
            SaveConfig();
        }

        public void UpdateFacilityId(FacilityType type, Guid actualId)
        {
            _dynamicFacilityIds[type] = actualId;
            Serilog.Log.Warning("[DIAG][FacilityContext] UpdateFacilityId({Type}, {Id}) called. Map now has {Count} entries.", type, actualId, _dynamicFacilityIds.Count);
            Serilog.Log.Information($"[FacilityContext] [RUNTIME DISCOVERY] Resolved Facility ID for {type}: {actualId}. This should be persisted to facility-config.json.");
            SaveConfig();
        }

        public FacilityContextService(IDispatcher dispatcher, ILocalizationService localizationService)
        {
            _dispatcher = dispatcher;
            _localizationService = localizationService;
            
            // FIX: Absolute path in %LOCALAPPDATA%\Luxurya
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var titanFolder = Path.Combine(localAppData, "Luxurya");
            if (!Directory.Exists(titanFolder)) Directory.CreateDirectory(titanFolder);
            
            _configPath = Path.Combine(titanFolder, "facility-config.json");

            // Subscribe to language changes to reload terminology
            _localizationService.LanguageChanged += (s, e) => RefreshResources();
        }

        private async void RefreshResources()
        {
            await SwitchFacility(CurrentFacility);
        }

        public void Initialize()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
                    var config = JsonSerializer.Deserialize<FacilityConfig>(json, options);
                    CurrentFacility = config?.InitialFacility ?? FacilityType.General;
                    LanguageCode = config?.LanguageCode ?? "en";
                    PublicSlug = config?.PublicSlug ?? string.Empty;
                    
                    if (config?.FacilityIds != null)
                    {
                        foreach (var mapping in config.FacilityIds)
                        {
                            _dynamicFacilityIds[mapping.Key] = mapping.Value;
                        }
                        Serilog.Log.Information("[FacilityContext] Loaded persisted facility-ID mappings. Pending CommitFacility.");
                    }
                }
                else
                {
                    CurrentFacility = FacilityType.General;
                    Serilog.Log.Information("[FacilityContext] No config file found. Initializing with General type. Pending CommitFacility.");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[FacilityContext] Failed to load config. Using General default.");
                CurrentFacility = FacilityType.General;
            }

            // NOTE: SwitchFacility is intentionally NOT called here.
            // CommitFacility() must be called by the host (App.xaml.cs) AFTER
            // auto-discovery has fully populated _dynamicFacilityIds with real GUIDs.
        }

        /// <summary>
        /// Finalises the facility switch after auto-discovery has populated the ID map.
        /// Must be called exactly once by App.xaml.cs after UpdateFacilities / UpdateFacilityId.
        /// </summary>
        public async void CommitFacility()
        {
            Serilog.Log.Information("[FacilityContext] CommitFacility called. CurrentFacility={Type} CurrentFacilityId={Id}", CurrentFacility, CurrentFacilityId);
            await SwitchFacility(CurrentFacility);
        }

        public async Task SwitchFacility(FacilityType type)
        {
            CurrentFacility = type;

            await _dispatcher.InvokeAsync(() =>
            {
                var appResources = System.Windows.Application.Current.Resources;
                
                // 1. Identify and remove existing facility themes (Branding and Terminology)
                // Use a more robust check for facility-specific dictionaries
                var toRemove = new List<System.Windows.ResourceDictionary>();
                foreach (var dict in appResources.MergedDictionaries)
                {
                    if (dict.Source == null) continue;
                    
                    var source = dict.Source.OriginalString;
                    if (source.Contains("Resources/Branding.") || 
                        (source.Contains("Resources/Terminology.") && !source.Contains("Terminology.Base.xaml")))
                    {
                        toRemove.Add(dict);
                    }
                }

                foreach (var dict in toRemove)
                {
                    Serilog.Log.Information("[FacilityContext] Removing stale resource: {Source}", dict.Source);
                    appResources.MergedDictionaries.Remove(dict);
                }

                // 2. Load and add new dictionaries
                // Guard: Skip resource loading for 'General' type — no Branding.General.xaml exists.
                if (type == Management.Domain.Enums.FacilityType.General)
                {
                    Serilog.Log.Information("[FacilityContext] Skipping branding load for 'General' type (no resource file).");
                    return;
                }

                try
                {
                    // Add Branding
                    string brandingPath = $"Resources/Branding.{type}.xaml";
                    appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                    { 
                        Source = new Uri(brandingPath, UriKind.Relative) 
                    });

                    // Add Terminology
                    var lang = _localizationService.CurrentCulture.TwoLetterISOLanguageName;
                    string terminologyPath = $"Resources/Terminology.{type}.xaml";
                    
                    // Localization: Check for localized filename convention (e.g. Terminology.Salon.fr.xaml)
                    // Note: In a production app, we'd verify path existence or use an asset manifest.
                    // For now, we try localized first, then fallback.
                    if (lang != "en")
                    {
                        string localizedPath = $"Resources/Terminology.{type}.{lang}.xaml";
                        try 
                        {
                             // Try to check if resource exists by creating it (WPF Uri check is tricky, but adding to MergedDictionaries works or throws)
                             appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                             { 
                                 Source = new Uri(localizedPath, UriKind.Relative) 
                             });
                             Serilog.Log.Information("[FacilityContext] Loaded localized terminology: {Source}", localizedPath);
                        }
                        catch 
                        {
                            // Fallback to default terminology
                            appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                            { 
                                Source = new Uri(terminologyPath, UriKind.Relative) 
                            });
                            Serilog.Log.Warning("[FacilityContext] Localized terminology not found for {Lang}, falling back to default.", lang);
                        }
                    }
                    else 
                    {
                        appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                        { 
                            Source = new Uri(terminologyPath, UriKind.Relative) 
                        });
                    }
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
            });

            // 3. Persist selection (always, even if we abort the event below)
            SaveConfig();

            // Step 4 SAFETY GUARD: Never fire FacilityChanged with an empty GUID.
            // This prevents ViewModels from executing their first query against a Guid.Empty filter
            // which would return zero results. The type is already persisted above, so the next
            // CommitFacility() call (e.g. after login discovery) will fire correctly.
            if (CurrentFacilityId == Guid.Empty)
            {
                Serilog.Log.Warning("[FacilityContext] GUARD: SwitchFacility({Type}) — CurrentFacilityId is Guid.Empty. FacilityChanged suppressed.", type);
                return;
            }

            Serilog.Log.Information("[FacilityContext] FacilityChanged firing. CurrentFacility={Facility} CurrentFacilityId={Id}", CurrentFacility, CurrentFacilityId);
            FacilityChanged?.Invoke(type);
        }

        private readonly System.Threading.SemaphoreSlim _configSaveLock = new(1, 1);

        private void SaveConfig()
        {
            // Use a semaphore to prevent concurrent file write races (fixes file-lock IOException).
            if (!_configSaveLock.Wait(0)) return; // Skip if a save is already in progress
            try
            {
                var config = new FacilityConfig 
                { 
                    InitialFacility = CurrentFacility,
                    LanguageCode = LanguageCode,
                    PublicSlug = PublicSlug,
                    FacilityIds = _dynamicFacilityIds.ToDictionary(k => k.Key, v => v.Value)
                };
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                var json = JsonSerializer.Serialize(config, options);
                var tempPath = _configPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _configPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[FacilityContext] Failed to save config to {Path}", _configPath);
            }
            finally
            {
                _configSaveLock.Release();
            }
        }
    }
}
