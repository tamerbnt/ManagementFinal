using System;
using System.Collections.Concurrent;
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
        public Guid? TenantId { get; set; }
        
        // Removed [JsonConverter(typeof(JsonStringEnumConverter))] as it crashes when applied directly to a Dictionary<Enum, Guid> property
        public Dictionary<FacilityType, Guid>? FacilityIds { get; set; }
    }

    public class FacilityContextService : Management.Domain.Services.IFacilityContextService
    {
        private readonly IDispatcher _dispatcher;
        private readonly ILocalizationService _localizationService;
        private readonly IOnboardingStateStore _onboardingState;
        private readonly string _configPath;
        
        // Default seed IDs removed (Relying on discovery)
        private readonly ConcurrentDictionary<FacilityType, Guid> _dynamicFacilityIds = new();

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

        /// <summary>
        /// Switches the active facility in memory and fires FacilityChanged, but does NOT
        /// write to facility-config.json. Call PersistFacilityChoice() after auth succeeds.
        /// Used by ChangeFacility() in MainViewModel to stage a switch before authentication.
        /// </summary>
        public async Task SetActiveFacility(FacilityType type)
        {
            Serilog.Log.Information("[FacilityContext] SetActiveFacility({Type}) — in-memory switch, no disk write.", type);
            await SwitchFacilityInMemory(type);
        }

        /// <summary>
        /// Persists the current facility selection to facility-config.json.
        /// Must only be called AFTER authentication has been confirmed.
        /// </summary>
        public void PersistFacilityChoice(FacilityType type)
        {
            CurrentFacility = type;
            Serilog.Log.Information("[FacilityContext] PersistFacilityChoice({Type}) — writing to disk.", type);
            SaveConfig();
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
                _dynamicFacilityIds.AddOrUpdate(mapping.Key, mapping.Value, (k, v) => mapping.Value);
                Serilog.Log.Information($"[FacilityContext] Updated {mapping.Key} to {mapping.Value}");
            }
            SaveConfig();
        }

        public void UpdateFacilityId(FacilityType type, Guid actualId)
        {
            _dynamicFacilityIds.AddOrUpdate(type, actualId, (k, v) => actualId);
            Serilog.Log.Warning("[DIAG][FacilityContext] UpdateFacilityId({Type}, {Id}) called. Map now has {Count} entries.", type, actualId, _dynamicFacilityIds.Count);
            Serilog.Log.Information($"[FacilityContext] [RUNTIME DISCOVERY] Resolved Facility ID for {type}: {actualId}. This should be persisted to facility-config.json.");
            SaveConfig();
        }

        public void SaveTenantId(Guid tenantId)
        {
            _onboardingState.TargetTenantId = tenantId;
            Serilog.Log.Information("[FacilityContext] Tenant ID saved to config: {Id}", tenantId);
            SaveConfig();
        }

        public FacilityContextService(IDispatcher dispatcher, ILocalizationService localizationService, IOnboardingStateStore onboardingState)
        {
            _dispatcher = dispatcher;
            _localizationService = localizationService;
            _onboardingState = onboardingState;
            
            // FIX: Absolute path in %PROGRAMDATA%\Luxurya
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var titanFolder = Path.Combine(programData, "Luxurya");
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
                    
                    if (config?.TenantId != null)
                    {
                        _onboardingState.TargetTenantId = config.TenantId;
                        Serilog.Log.Information("[FacilityContext] Restored TargetTenantId from config: {Id}", config.TenantId);
                    }

                    if (config?.FacilityIds != null)
                    {
                        foreach (var mapping in config.FacilityIds)
                        {
                            _dynamicFacilityIds.TryAdd(mapping.Key, mapping.Value);
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

        public Guid GetFacilityId(FacilityType type)
        {
            return _dynamicFacilityIds.GetValueOrDefault(type, Guid.Empty);
        }

        public async Task SwitchFacility(FacilityType type)
        {
            CurrentFacility = type;
            await LoadFacilityResourcesAsync(type);

            // Persist selection to disk (used by normal facility commit path)
            SaveConfig();

            // Safety guard: Never fire FacilityChanged with an empty GUID
            if (CurrentFacilityId == Guid.Empty)
            {
                Serilog.Log.Warning("[FacilityContext] GUARD: SwitchFacility({Type}) — CurrentFacilityId is Guid.Empty. FacilityChanged suppressed.", type);
                return;
            }

            Serilog.Log.Information("[FacilityContext] FacilityChanged firing. CurrentFacility={Facility} CurrentFacilityId={Id}", CurrentFacility, CurrentFacilityId);
            FacilityChanged?.Invoke(type);
        }

        /// <summary>
        /// Switches facility resources and fires FacilityChanged WITHOUT saving to disk.
        /// Used by SetActiveFacility() to stage a switch during the auth flow.
        /// </summary>
        private async Task SwitchFacilityInMemory(FacilityType type)
        {
            CurrentFacility = type;
            await LoadFacilityResourcesAsync(type);

            // Safety guard: Never fire FacilityChanged with an empty GUID
            if (CurrentFacilityId == Guid.Empty)
            {
                Serilog.Log.Warning("[FacilityContext] GUARD: SwitchFacilityInMemory({Type}) — CurrentFacilityId is Guid.Empty. FacilityChanged suppressed.", type);
                return;
            }

            Serilog.Log.Information("[FacilityContext] FacilityChanged firing (in-memory). CurrentFacility={Facility}", CurrentFacility);
            FacilityChanged?.Invoke(type);
        }

        /// <summary>
        /// Loads branding and terminology resources for the given facility type.
        /// Extracted from SwitchFacility to allow reuse by both persisted and in-memory switches.
        /// </summary>
        private async Task LoadFacilityResourcesAsync(FacilityType type)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                var appResources = System.Windows.Application.Current.Resources;
                
                // 1. Remove existing facility themes (Branding and Terminology)
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

                // 2. Load new dictionaries
                if (type == Management.Domain.Enums.FacilityType.General)
                {
                    Serilog.Log.Information("[FacilityContext] Skipping branding load for 'General' type (no resource file).");
                    return;
                }

                try
                {
                    string brandingPath = $"Resources/Branding.{type}.xaml";
                    appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                    { 
                        Source = new Uri(brandingPath, UriKind.Relative) 
                    });

                    var lang = _localizationService.CurrentCulture.TwoLetterISOLanguageName;
                    string terminologyPath = $"Resources/Terminology.{type}.xaml";
                    
                    if (lang != "en")
                    {
                        string localizedPath = $"Resources/Terminology.{type}.{lang}.xaml";
                        try 
                        {
                             appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary 
                             { 
                                 Source = new Uri(localizedPath, UriKind.Relative) 
                             });
                             Serilog.Log.Information("[FacilityContext] Loaded localized terminology: {Source}", localizedPath);
                        }
                        catch 
                        {
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
                    TenantId = _onboardingState.TargetTenantId,
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
