using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using Management.Domain.Enums;

namespace Management.Presentation.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    /// <summary>
    /// Identifies which light-mode palette variant to apply per facility.
    /// Default  = the current modern palette (Salon: Midnight Blush, Gym: Arctic).
    /// Alternate = the warm/classic recovered palette (Salon: Warm Linen, Gym: Steel).
    /// </summary>
    public enum LightPalette
    {
        Default,
        Alternate,
        Classic,
        Atrium,
        NoirBlush
    }

    public static class ThemeManager
    {
        private static AppTheme _currentTheme = AppTheme.Light;
        private static FacilityType _currentFacility = FacilityType.Gym;
        private static LightPalette _currentLightPalette = LightPalette.Default;

        // ── Public API ───────────────────────────────────────────────────────────

        public static AppTheme CurrentTheme => _currentTheme;
        public static FacilityType CurrentFacility => _currentFacility;
        public static LightPalette CurrentLightPalette => _currentLightPalette;

        public static void SetTheme(AppTheme theme, FacilityType? facility = null)
        {
            _currentTheme = theme;
            var themeUri = theme == AppTheme.Light
                ? new Uri("Resources/Theme.Light.xaml", UriKind.Relative)
                : new Uri("Resources/Theme.Dark.xaml", UriKind.Relative);

            UpdateDictionary("Theme.", themeUri);

            // Re-apply facility branding to ensure the theme-specific variant is loaded.
            // Use the provided facility if available, otherwise fall back to the last known one.
            SetFacility(facility ?? _currentFacility);

            // Persist locally so the preference survives logout/restart without needing the DB.
            SavePrefs();
        }

        public static void SetFacility(FacilityType facility)
        {
            _currentFacility = facility;
            string themeSuffix = _currentTheme == AppTheme.Dark ? ".Dark" : "";
            Uri facilityUri;

            switch (facility)
            {
                case FacilityType.Salon:
                    facilityUri = new Uri($"Resources/Branding.Salon{themeSuffix}.xaml", UriKind.Relative);
                    break;
                case FacilityType.Restaurant:
                    facilityUri = new Uri($"Resources/Branding.Restaurant{themeSuffix}.xaml", UriKind.Relative);
                    break;
                case FacilityType.Gym:
                default:
                    facilityUri = new Uri($"Resources/Branding.Gym{themeSuffix}.xaml", UriKind.Relative);
                    break;
            }

            UpdateDictionary("Branding.", facilityUri);

            // After branding is set, apply (or remove) the light palette overlay.
            ApplyLightPaletteOverlay();
        }

        /// <summary>
        /// Switches the light-mode palette variant for the current facility.
        /// Has no visible effect while Dark Mode is active — the preference is
        /// stored and automatically restored when returning to Light Mode.
        /// </summary>
        public static void SetLightPalette(LightPalette palette)
        {
            _currentLightPalette = palette;
            ApplyLightPaletteOverlay();

            // Persist locally so the preference survives logout/restart without needing the DB.
            SavePrefs();
        }

        // ── Local Preference Persistence ─────────────────────────────────────────

        private static readonly string _prefsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Atrium",
            "theme-prefs.json");

        /// <summary>
        /// Writes the current theme and palette to a tiny JSON file on disk.
        /// Called automatically by SetTheme() and SetLightPalette().
        /// </summary>
        private static void SavePrefs()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(_prefsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(new
                {
                    Theme   = _currentTheme.ToString(),
                    Palette = _currentLightPalette.ToString()
                }, new JsonSerializerOptions { WriteIndented = false });

                File.WriteAllText(_prefsPath, json);
            }
            catch
            {
                // Non-critical — silently ignore write failures.
            }
        }

        /// <summary>
        /// Reads the persisted theme and palette from disk.
        /// Returns (null, null) if the file doesn't exist or cannot be parsed.
        /// </summary>
        public static (AppTheme? Theme, LightPalette? Palette) LoadPrefs()
        {
            try
            {
                if (!File.Exists(_prefsPath)) return (null, null);

                using var doc = JsonDocument.Parse(File.ReadAllText(_prefsPath));
                var root = doc.RootElement;

                AppTheme? theme = null;
                if (root.TryGetProperty("Theme", out var t) &&
                    Enum.TryParse<AppTheme>(t.GetString(), out var parsedTheme))
                    theme = parsedTheme;

                LightPalette? palette = null;
                if (root.TryGetProperty("Palette", out var p) &&
                    Enum.TryParse<LightPalette>(p.GetString(), out var parsedPalette))
                    palette = parsedPalette;

                return (theme, palette);
            }
            catch
            {
                return (null, null);
            }
        }

        // ── Private Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Loads the correct palette overlay XAML when in Light Mode,
        /// or removes it entirely when in Dark Mode.
        /// Prefix used in filename: "Palette." — distinct from "Theme." and "Branding.".
        /// </summary>
        private static void ApplyLightPaletteOverlay()
        {
            string fileName = ResolvePaletteFileName(_currentFacility, _currentLightPalette);
            
            if (string.IsNullOrEmpty(fileName)) 
            {
                var emptyUri = new Uri("Resources/Palette.Empty.xaml", UriKind.Relative);
                UpdateDictionary("Palette.", emptyUri);
                return;
            }

            if (_currentTheme == AppTheme.Dark)
            {
                // For Gym and Salon, we have created .Dark.xaml variants that ONLY contain the accent colors,
                // allowing us to keep the selected palette's accent color in dark mode without overriding dark backgrounds.
                if (_currentFacility == FacilityType.Gym || _currentFacility == FacilityType.Salon || _currentFacility == FacilityType.General)
                {
                    fileName = fileName.Replace(".xaml", ".Dark.xaml");
                }
                else
                {
                    // Other facilities don't have custom palettes or dark overrides, so we clear.
                    var emptyUri = new Uri("Resources/Palette.Empty.xaml", UriKind.Relative);
                    UpdateDictionary("Palette.", emptyUri);
                    return;
                }
            }

            var uri = new Uri($"Resources/{fileName}", UriKind.Relative);
            UpdateDictionary("Palette.", uri);
        }

        private static string ResolvePaletteFileName(FacilityType facility, LightPalette palette)
        {
            // Fallback General to Gym so it matches the Branding fallback
            if (facility == FacilityType.General) facility = FacilityType.Gym;

            return (facility, palette) switch
            {
                (FacilityType.Salon, LightPalette.Default)   => "Palette.Salon.Blush.xaml",
                (FacilityType.Salon, LightPalette.Alternate) => "Palette.Salon.Linen.xaml",
                (FacilityType.Salon, LightPalette.Atrium)    => "Palette.Salon.Atrium.xaml",
                (FacilityType.Salon, LightPalette.NoirBlush) => "Palette.Salon.NoirBlush.xaml",
                (FacilityType.Gym,   LightPalette.Default)   => "Palette.Gym.Arctic.xaml",
                (FacilityType.Gym,   LightPalette.Alternate) => "Palette.Gym.Steel.xaml",
                (FacilityType.Gym,   LightPalette.Classic)   => "Palette.Gym.Gold.xaml",
                (FacilityType.Gym,   LightPalette.Atrium)    => "Palette.Gym.Atrium.xaml",
                _ => string.Empty
            };
        }

        private static void UpdateDictionary(string sourcePart, Uri newResourceUri)
        {
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;

            // Search for existing dictionary with this prefix
            for (int i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.OriginalString;
                if (source != null && source.Contains(sourcePart))
                {
                    // Found an existing entry with this prefix (e.g., "Branding." or "Palette.").
                    // Use RemoveAt + Insert with a FRESH dictionary object — not WPF's SharedDictionaryManager
                    // cache — to guarantee DynamicResource bindings are always fully invalidated and
                    // re-evaluated by the visual tree on every swap.
                    dictionaries.RemoveAt(i);
                    dictionaries.Insert(i, LoadFreshDictionary(newResourceUri));
                    return;
                }
            }

            // Not found — add as a new entry at the end (highest priority wins in WPF).
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(
                LoadFreshDictionary(newResourceUri));
        }

        /// <summary>
        /// Loads a ResourceDictionary by parsing its compiled BAML stream directly via
        /// <see cref="XamlReader.Load"/>, bypassing WPF's internal SharedDictionaryManager cache.
        /// This guarantees a brand-new object reference on every call so that
        /// <c>RemoveAt + Insert</c> always triggers a full DynamicResource invalidation —
        /// even when the same pack URI is applied twice in rapid succession (e.g., the
        /// startup double-apply that occurs because SetLightPalette + SetTheme both call
        /// ApplyLightPaletteOverlay, or when cycling back to a previously-loaded palette).
        /// Falls back to the standard cached load if the BAML stream cannot be obtained.
        /// </summary>
        private static ResourceDictionary LoadFreshDictionary(Uri relativeUri)
        {
            try
            {
                // Build absolute pack URI from the relative resource path.
                var packUri = new Uri(
                    $"pack://application:,,,/{relativeUri.OriginalString}",
                    UriKind.Absolute);

                var streamInfo = System.Windows.Application.GetResourceStream(packUri);
                if (streamInfo?.Stream != null)
                {
                    using var stream = streamInfo.Stream;
                    var dict = (ResourceDictionary)XamlReader.Load(stream);
                    // Restore Source so UpdateDictionary's prefix search can find this
                    // dictionary on future swaps. XamlReader.Load leaves Source=null,
                    // which would make the slot invisible to the loop and cause Add()
                    // to accumulate orphaned palette dicts instead of replacing in-place.
                    dict.Source = relativeUri;
                    return dict;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(
                    ex,
                    "[ThemeManager] Could not load fresh dictionary for {Uri}. Falling back to cached load.",
                    relativeUri);
            }

            // Fallback: standard cached load if the BAML stream is unavailable.
            return new ResourceDictionary { Source = relativeUri };
        }


    }
}
