using System.Collections.Generic;
using System.Globalization;

namespace Management.Presentation.Services.Localization
{
    public interface ILocalizationService
    {
        event System.EventHandler LanguageChanged;
        CultureInfo CurrentCulture { get; }
        void SetLanguage(string languageCode); // "en", "fr", "ar"
        IEnumerable<CultureInfo> SupportedLanguages { get; }
        string GetString(string key);
    }
}
