using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.Services.Localization
{
    public class LocalizationService : ILocalizationService
    {
        private const string StringsResourcePrefix = "Strings.";
        private const string StringsResourceFolder = "Resources/Localization/";
        private readonly IDispatcher _dispatcher;
        private readonly IServiceProvider _serviceProvider;

        public CultureInfo CurrentCulture { get; private set; } = new CultureInfo("en");

        public IEnumerable<CultureInfo> SupportedLanguages { get; } = new List<CultureInfo>
        { 
            new CultureInfo("en"),
            new CultureInfo("fr"),
            new CultureInfo("ar")
        };

        public LocalizationService(IDispatcher dispatcher, IServiceProvider serviceProvider)
        {
            _dispatcher = dispatcher;
            _serviceProvider = serviceProvider;
        }

        public event EventHandler LanguageChanged;

        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) throw new ArgumentNullException(nameof(languageCode));

            _dispatcher.InvokeAsync(() =>
            {
                var culture = new CultureInfo(languageCode);
                CurrentCulture = culture;

                // 1. Update Thread Culture (Ensures UI thread has correct culture for resource loading)
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                // 2. Load Resource Dictionary
                UpdateResourceDictionaryInternal(languageCode);

                // 3. Notify Subscribers
                LanguageChanged?.Invoke(this, EventArgs.Empty);

                // 4. Persist preferred language (lazy resolution prevents circular dependency)
                var facilityContext = _serviceProvider.GetRequiredService<IFacilityContextService>();
                facilityContext.SaveLanguage(languageCode);
            });
        }

        public string GetString(string key)
        {
            if (System.Windows.Application.Current.Resources.Contains(key))
            {
                return System.Windows.Application.Current.Resources[key] as string;
            }
            return $"[{key}]";
        }

        private void UpdateResourceDictionaryInternal(string languageCode)
        {
            var appResources = System.Windows.Application.Current.Resources;
            
            // 1. Identify and remove any PREVIOUSLY added localized dictionaries
            // (Looking for .ar.xaml or .fr.xaml suffixes)
            var localizedDicts = appResources.MergedDictionaries
                .Where(d => d.Source != null && 
                           (d.Source.OriginalString.Contains(".ar.xaml") || 
                            d.Source.OriginalString.Contains(".fr.xaml")))
                .ToList();

            foreach (var dict in localizedDicts)
            {
                appResources.MergedDictionaries.Remove(dict);
            }

            // 2. If the target language is English, we're done (App.xaml has English base)
            if (languageCode == "en")
            {
                Serilog.Log.Information("Reverted to base English strings");
                return;
            }

            // 3. Define and Load Localized Overlays
            // We overlay the generic Strings AND any facility-specific terminology that exists
            var overlayConfigs = new List<(string Name, string Folder)>
            {
                ("Strings", "Resources/Localization/"),
                ("Terminology.Base", "Resources/"),
                ("Terminology.Gym", "Resources/"),
                ("Terminology.Salon", "Resources/")
            };

            foreach (var config in overlayConfigs)
            {
                try
                {
                    string uriPath = $"{config.Folder}{config.Name}.{languageCode}.xaml";
                    var dictionaryUri = new Uri(uriPath, UriKind.Relative);
                    
                    // WPF throws if file doesn't exist, so we use a try-catch for safe fallback
                    var newDictionary = new ResourceDictionary { Source = dictionaryUri };
                    
                    // Add it at the END (precedence based layering)
                    appResources.MergedDictionaries.Add(newDictionary);
                    Serilog.Log.Debug("Overlayed {Name} for {LanguageCode}", config.Name, languageCode);
                }
                catch
                {
                    // If a file is missing (e.g. Gym.ar.xaml not created yet), 
                    // we just log it and move on. The UI will show the English fallback from App.xaml.
                    Serilog.Log.Debug("No localized dictionary found for {Name} in {LanguageCode}", 
                        config.Name, languageCode);
                }
            }
        }

    }
}
