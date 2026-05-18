using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Presentation.ViewModels.Auth;
using Management.Application.Interfaces;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;
using Management.Application.Services;

namespace Management.Presentation.ViewModels
{
    public class PreferencesSetupViewModel : FacilityAwareViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;

        private bool _isDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    ThemeManager.SetTheme(value ? AppTheme.Dark : AppTheme.Light);
                    OnPropertyChanged(nameof(IsLightTheme));
                }
            }
        }

        public bool IsLightTheme
        {
            get => !IsDarkTheme;
            set => IsDarkTheme = !value;
        }

        private string _selectedLanguage = "en";
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    _localizationService.SetLanguage(value);
                    OnPropertyChanged(nameof(IsLanguageEnglish));
                    OnPropertyChanged(nameof(IsLanguageFrench));
                    OnPropertyChanged(nameof(IsLanguageArabic));
                }
            }
        }

        public bool IsLanguageEnglish => SelectedLanguage == "en";
        public bool IsLanguageFrench => SelectedLanguage == "fr";
        public bool IsLanguageArabic => SelectedLanguage == "ar";

        // --- Terminology / Localization Labels ---
        public string TitleLabel => _localizationService?.GetString("Strings.Auth.PreferencesTitle") ?? "Personalize Your Workspace";
        public string SubtitleLabel => _localizationService?.GetString("Strings.Auth.PreferencesSubtitle") ?? "Choose your preferred theme and language to get started.";
        public string ThemeSectionLabel => _localizationService?.GetString("Strings.Auth.ThemeSelection") ?? "WORKSPACE MODE";
        public string LanguageSectionLabel => _localizationService?.GetString("Strings.Auth.LanguageSelection") ?? "INTERFACE LANGUAGE";
        public string LightThemeLabel => _localizationService?.GetString("Strings.Auth.Theme.Light") ?? "Light Mode";
        public string DarkThemeLabel => _localizationService?.GetString("Strings.Auth.Theme.Dark") ?? "Dark Mode";
        public string ActionButtonLabel => _localizationService?.GetString("Strings.Auth.Action.ContinueToWorkspace") ?? "Continue to Workspace";

        protected override void OnLanguageChanged()
        {
            OnPropertyChanged(nameof(TitleLabel));
            OnPropertyChanged(nameof(SubtitleLabel));
            OnPropertyChanged(nameof(ThemeSectionLabel));
            OnPropertyChanged(nameof(LanguageSectionLabel));
            OnPropertyChanged(nameof(LightThemeLabel));
            OnPropertyChanged(nameof(DarkThemeLabel));
            OnPropertyChanged(nameof(ActionButtonLabel));
        }

        public ICommand SelectThemeCommand { get; }
        public ICommand SelectLanguageCommand { get; }
        public ICommand ContinueCommand { get; }

        public PreferencesSetupViewModel(
            INavigationService navigationService,
            IDialogService dialogService,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILocalizationService localizationService,
            ILogger<PreferencesSetupViewModel> logger,
            IDiagnosticService diagnosticService)
            : base(terminologyService, facilityContext, logger, diagnosticService, null, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;

            // Load initial theme preference from ThemeManager
            _isDarkTheme = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Load initial language preference from LocalizationService
            if (_localizationService.CurrentCulture != null)
            {
                var lang = _localizationService.CurrentCulture.TwoLetterISOLanguageName;
                _selectedLanguage = (lang == "ar" || lang == "fr") ? lang : "en";
            }

            SelectThemeCommand = new RelayCommand<string>(ExecuteSelectTheme);
            SelectLanguageCommand = new RelayCommand<string>(ExecuteSelectLanguage);
            ContinueCommand = new AsyncRelayCommand(ExecuteContinueAsync);
        }

        private void ExecuteSelectTheme(string theme)
        {
            IsDarkTheme = (theme == "Dark");
        }

        private void ExecuteSelectLanguage(string language)
        {
            SelectedLanguage = language;
        }

        private async Task ExecuteContinueAsync()
        {
            await _navigationService.NavigateToAsync<SplashOnboardingViewModel>();
        }
    }
}
