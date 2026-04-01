using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces.App;
using Management.Application.Interfaces.ViewModels;
using Management.Domain.Enums;
using Management.Infrastructure.Data;
using Management.Presentation.Services;
using Management.Presentation.ViewModels.Base;

namespace Management.Presentation.ViewModels.Auth
{
    public class SplashOnboardingViewModel : FacilityAwareViewModelBase, IAsyncViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly AppDbContext _dbContext;
        private readonly IAppInitializationTracker _initTracker;
        private readonly IDispatcher _dispatcher;

        public ObservableCollection<OnboardingSlide> Slides { get; } = new();
        
        public string CurrentActionColor => Slides.Count > CurrentSlideIndex && CurrentSlideIndex >= 0 ? Slides[CurrentSlideIndex].TitleColor : "#000000";

        private int _currentSlideIndex;
        public int CurrentSlideIndex
        {
            get => _currentSlideIndex;
            set 
            {
                if (SetProperty(ref _currentSlideIndex, value))
                {
                    UpdateSlideSelection();
                    OnPropertyChanged(nameof(CurrentActionColor));
                }
            }
        }

        private void UpdateSlideSelection()
        {
            for (int i = 0; i < Slides.Count; i++)
            {
                Slides[i].IsSelected = (i == CurrentSlideIndex);
            }
        }

        private ObservableCollection<FacilityTypeOption> _availableFacilities = new();
        public ObservableCollection<FacilityTypeOption> AvailableFacilities
        {
            get => _availableFacilities;
            set => SetProperty(ref _availableFacilities, value);
        }

        private FacilityTypeOption? _selectedFacility;
        public FacilityTypeOption? SelectedFacility
        {
            get => _selectedFacility;
            set 
            {
                if (SetProperty(ref _selectedFacility, value))
                {
                    foreach (var option in AvailableFacilities)
                    {
                        option.IsSelected = (option == value);
                    }
                    EnterWorkspaceCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAppInitializationTracker InitTracker => _initTracker;

        public ICommand NextSlideCommand { get; }
        public ICommand PrevSlideCommand { get; }
        public AsyncRelayCommand EnterWorkspaceCommand { get; }
        public ICommand SelectFacilityCommand { get; }

        public SplashOnboardingViewModel(
            INavigationService navigationService,
            AppDbContext dbContext,
            IAppInitializationTracker initTracker,
            Management.Presentation.Services.Localization.ILocalizationService localizationService,
            Management.Domain.Services.ITerminologyService terminologyService,
            Management.Domain.Services.IFacilityContextService facilityContext,
            Management.Domain.Services.IDialogService dialogService,
            Management.Application.Interfaces.App.IToastService toastService,
            ILogger<SplashOnboardingViewModel> logger,
            Management.Application.Services.IDiagnosticService diagnosticService,
            IDispatcher dispatcher)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService, dialogService)
        {
            _navigationService = navigationService;
            _dbContext = dbContext;
            _initTracker = initTracker;
            _dispatcher = dispatcher;

            NextSlideCommand = new RelayCommand(() => CurrentSlideIndex = (CurrentSlideIndex + 1) % Slides.Count);
            PrevSlideCommand = new RelayCommand(() => CurrentSlideIndex = (CurrentSlideIndex - 1 + Slides.Count) % Slides.Count);
            
            EnterWorkspaceCommand = new AsyncRelayCommand(ExecuteEnterWorkspace, CanExecuteEnterWorkspace);
            SelectFacilityCommand = new RelayCommand<FacilityTypeOption>(f => SelectedFacility = f);

            InitializeSlides();

            // FIX: Force data execution instantly on instantiation.
            // Bypasses the Navigation pipeline which is intentionally skipped natively by App.xaml.cs startup routing.
            _ = LoadFacilitiesFromLocalAsync();
        }

        private void InitializeSlides()
        {
            Slides.Clear();
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Ditch the chaos.", 
                TechnicalSubtitle = "Automate your daily operations and focus on what matters most.",
                ImagePath = "pack://application:,,,/Luxurya.Client;component/Resources/Images/onboarding_chaos_v2.png",
                TitleColor = "#FACC15",
                SubtitleColor = "#FFFFFF",
                BackgroundColor = "#000000"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Frictionless entry.", 
                TechnicalSubtitle = "Secure RFID access control fully integrated with your member database.",
                ImagePath = "pack://application:,,,/Luxurya.Client;component/Resources/Images/onboarding_access_v2.png",
                TitleColor = "#FACC15",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#FFFFFF"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Master your schedule.", 
                TechnicalSubtitle = "Drag-and-drop bookings with real-time availability and automatic reminders.",
                ImagePath = "pack://application:,,,/Luxurya.Client;component/Resources/Images/onboarding_sched_v2.png",
                TitleColor = "#FFFFFF",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#06B6D4"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Growth, visualized.", 
                TechnicalSubtitle = "Deep insights into your revenue and facility performance metrics.",
                ImagePath = "pack://application:,,,/Luxurya.Client;component/Resources/Images/onboarding_growth_v2.png",
                TitleColor = "#FFFFFF",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#F48FB1"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Reliable. No matter what.", 
                TechnicalSubtitle = "Stay productive in offline mode with instant cloud-sync when reconnected.",
                ImagePath = "pack://application:,,,/Luxurya.Client;component/Resources/Images/onboarding_sync_v2.png",
                TitleColor = "#FFFFFF",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#F87171"
            });

            UpdateSlideSelection();
        }

        private bool CanExecuteEnterWorkspace()
        {
            return SelectedFacility != null && _initTracker.IsComplete;
        }

        private async Task ExecuteEnterWorkspace()
        {
            if (SelectedFacility == null) return;
            
            // Navigate to Login, passing the selected facility as context
            await _navigationService.NavigateToAsync<LoginViewModel>(SelectedFacility);
        }

        public async Task InitializeAsync()
        {
            await LoadFacilitiesFromLocalAsync();
        }

        private async Task LoadFacilitiesFromLocalAsync()
        {
            try
            {
                var localFacilities = await _dbContext.Facilities
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(f => !f.IsDeleted)
                    .ToListAsync();

                if (localFacilities.Count > 0)
                {
                    var options = localFacilities
                        .GroupBy(f => f.Type)
                        .Select(g => g.First())
                        .Select(f => new FacilityTypeOption
                        {
                            Id = f.Id,
                            Type = f.Type,
                            Name = f.Name,
                            Description = GetDescription(f.Type),
                            GradientStart = GetGradient(f.Type, true),
                            GradientEnd = GetGradient(f.Type, false),
                            IconKey = GetIcon(f.Type)
                        })
                        .ToList();

                    _dispatcher.Invoke(() => 
                    {
                        if (options.Count == 0)
                        {
                            // Missing Synced local DB. Injecting Fallback Discovery options for Splash Mockup usage.
                            options.Add(new FacilityTypeOption { Id = Guid.NewGuid(), Type = FacilityType.Gym, Name = "Titan Gym", Description = GetDescription(FacilityType.Gym), GradientStart = GetGradient(FacilityType.Gym, true), GradientEnd = GetGradient(FacilityType.Gym, false), IconKey = GetIcon(FacilityType.Gym) });
                            options.Add(new FacilityTypeOption { Id = Guid.NewGuid(), Type = FacilityType.Salon, Name = "Titan Salon", Description = GetDescription(FacilityType.Salon), GradientStart = GetGradient(FacilityType.Salon, true), GradientEnd = GetGradient(FacilityType.Salon, false), IconKey = GetIcon(FacilityType.Salon) });
                            options.Add(new FacilityTypeOption { Id = Guid.NewGuid(), Type = FacilityType.Restaurant, Name = "Titan Restaurant", Description = GetDescription(FacilityType.Restaurant), GradientStart = GetGradient(FacilityType.Restaurant, true), GradientEnd = GetGradient(FacilityType.Restaurant, false), IconKey = GetIcon(FacilityType.Restaurant) });
                        }
                        
                        AvailableFacilities.Clear();
                        foreach (var opt in options)
                        {
                            AvailableFacilities.Add(opt);
                        }
                        SelectedFacility = AvailableFacilities.FirstOrDefault(f => f.Type == FacilityType.Gym) ?? AvailableFacilities.FirstOrDefault();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load facilities for splash discovery.");
            }
        }

        private string GetDescription(FacilityType type)
        {
            return type switch
            {
                FacilityType.Gym => "Fitness & Wellness Analytics",
                FacilityType.Salon => "Beauty & Spa Operations",
                FacilityType.Restaurant => "Fine Dining Control",
                _ => "Titan Managed Workspace"
            };
        }

        private string GetGradient(FacilityType type, bool start)
        {
            return type switch
            {
                FacilityType.Gym => start ? "#0EA5E9" : "#2563EB",
                FacilityType.Salon => start ? "#F43F5E" : "#E11D48",
                FacilityType.Restaurant => start ? "#F59E0B" : "#D97706",
                _ => start ? "#64748B" : "#475569"
            };
        }

        private string GetIcon(FacilityType type)
        {
            return type switch
            {
                FacilityType.Gym => FacilityTypeOption.icon_gym,
                FacilityType.Salon => FacilityTypeOption.icon_salon,
                FacilityType.Restaurant => FacilityTypeOption.icon_restaurant,
                _ => FacilityTypeOption.icon_gym
            };
        }
    }
}
