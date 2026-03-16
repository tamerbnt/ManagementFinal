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
            try
            {
                var dictionaryUri = new Uri($"{StringsResourceFolder}Strings.{languageCode}.xaml", UriKind.Relative);
                var newDictionary = new ResourceDictionary { Source = dictionaryUri };

                // Find existing localization dictionary to replace
                var existingDictionary = System.Windows.Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings.") && d.Source.OriginalString.Contains("Localization"));

                if (existingDictionary != null)
                {
                    System.Windows.Application.Current.Resources.MergedDictionaries.Remove(existingDictionary);
                }

                // ADD to the end to ensure it overrides defaults
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(newDictionary);
                Serilog.Log.Information("Localization switched successfully to {LanguageCode}", languageCode);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to update ResourceDictionary for language {LanguageCode}", languageCode);
                throw; 
            }
        }
    }
}
