using CommunityToolkit.Mvvm.ComponentModel;
using Management.Presentation.Services;
using Management.Application.Stores;
using Management.Presentation.Stores;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Shared;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.ViewModels.Auth;

namespace Management.Presentation.ViewModels
{
    public partial class AuthViewModel : ViewModelBase, IDisposable
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly NavigationStore _navigationStore;
        private readonly INotificationService _notificationService;
        private readonly INavigationService _navigationService;

        public bool CanNavigateBack => _navigationStore.CanNavigateBack;
        public ICommand NavigateBackCommand { get; }

        private ViewModelBase? _currentView;
        public ViewModelBase? CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(CardScrollVisibility));
                }
            }
        }

        public System.Windows.Controls.ScrollBarVisibility CardScrollVisibility => 
            _currentView is SplashOnboardingViewModel 
                ? System.Windows.Controls.ScrollBarVisibility.Disabled 
                : System.Windows.Controls.ScrollBarVisibility.Auto;
        
        private object? _currentModal;
        public object? CurrentModal
        {
            get => _currentModal;
            set => SetProperty(ref _currentModal, value);
        }

        private bool _isModalOpen;
        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        // --- Slide Presentation Logic ---
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

        public ICommand NextSlideCommand { get; }
        public ICommand PrevSlideCommand { get; }
        // --------------------------------

        public System.Collections.ObjectModel.ObservableCollection<ToastViewModel> ActiveToasts => _notificationService.ActiveToasts;

        public AuthViewModel(
            ModalNavigationStore modalNavigationStore, 
            NavigationStore navigationStore, 
            INotificationService notificationService,
            INavigationService navigationService)
        {
            _modalNavigationStore = modalNavigationStore;
            _modalNavigationStore.PropertyChanged += OnModalStorePropertyChanged;

            _navigationStore = navigationStore;
            _notificationService = notificationService;
            _navigationService = navigationService;

            _navigationStore.BackStackChanged += OnBackStackChanged;
            NavigateBackCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ExecuteNavigateBackAsync);
            
            // ARCHITECTURE GUARD: Only allow Auth-related ViewModels in this shell.
            // This prevents the Dashboard from ever "Mashup" rendering inside the Auth card.
            var candidateVm = _navigationStore.CurrentViewModel as ViewModelBase;
            _currentView = IsAuthView(candidateVm) ? candidateVm : null;

            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;

            CurrentModal = _modalNavigationStore.CurrentModalViewModel;
            IsModalOpen = _modalNavigationStore.IsOpen;

            NextSlideCommand = new Management.Presentation.Extensions.RelayCommand(() => CurrentSlideIndex = (CurrentSlideIndex + 1) % Slides.Count);
            PrevSlideCommand = new Management.Presentation.Extensions.RelayCommand(() => CurrentSlideIndex = (CurrentSlideIndex - 1 + Slides.Count) % Slides.Count);

            InitializeSlides();
        }

        private void OnBackStackChanged()
        {
            OnPropertyChanged(nameof(CanNavigateBack));
        }

        private async Task ExecuteNavigateBackAsync()
        {
            await _navigationService.NavigateBackAsync();
        }

        private void UpdateSlideSelection()
        {
            for (int i = 0; i < Slides.Count; i++)
            {
                Slides[i].IsSelected = (i == CurrentSlideIndex);
            }
        }

        private void InitializeSlides()
        {
            Slides.Clear();
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Reliable. No matter what.", 
                TechnicalSubtitle = "Stay productive in offline mode with instant cloud-sync when reconnected.",
                ImagePath = "pack://application:,,,/Atrium.Client;component/Resources/Images/onboarding_sync_v2.png",
                TitleColor = "#FFFFFF",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#F87171"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Your empire, unified.", 
                TechnicalSubtitle = "Manage multiple branches and diverse business contexts seamlessly from a single, powerful command center.",
                ImagePath = "pack://application:,,,/Atrium.Client;component/Resources/Images/onboarding_nexus_v2.png",
                TitleColor = "#FFFFFF",
                SubtitleColor = "#FFFFFF",
                BackgroundColor = "#1A1510"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Master your schedule.", 
                TechnicalSubtitle = "Drag-and-drop bookings with real-time availability and automatic reminders.",
                ImagePath = "pack://application:,,,/Atrium.Client;component/Resources/Images/onboarding_sched_v2.png",
                TitleColor = "#FFFFFF",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#06B6D4"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Frictionless entry.", 
                TechnicalSubtitle = "Secure RFID access control fully integrated with your member database.",
                ImagePath = "pack://application:,,,/Atrium.Client;component/Resources/Images/onboarding_access_v2.png",
                TitleColor = "#FACC15",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#FFFFFF"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Ditch the chaos.", 
                TechnicalSubtitle = "Automate your daily operations and focus on what matters most.",
                ImagePath = "pack://application:,,,/Atrium.Client;component/Resources/Images/onboarding_chaos_v2.png",
                TitleColor = "#FACC15",
                SubtitleColor = "#FFFFFF",
                BackgroundColor = "#000000"
            });
            Slides.Add(new OnboardingSlide 
            { 
                EmotionalHeadline = "Growth, visualized.", 
                TechnicalSubtitle = "Deep insights into your revenue and facility performance metrics.",
                ImagePath = "pack://application:,,,/Atrium.Client;component/Resources/Images/onboarding_growth_v2.png",
                TitleColor = "#FFFFFF",
                SubtitleColor = "#0F172A",
                BackgroundColor = "#F48FB1"
            });

            UpdateSlideSelection();
        }

        private bool _isHandoffInProgress;

        public void PrepareForHandoff()
        {
            Serilog.Log.Information("[AuthViewModel] Handoff initiated. Disconnecting from NavigationStore and clearing view...");
            _isHandoffInProgress = true;
            _navigationStore.CurrentViewModelChanged -= OnCurrentViewModelChanged;
            _navigationStore.BackStackChanged -= OnBackStackChanged;
            CurrentView = null;
        }

        private void OnCurrentViewModelChanged()
        {
            if (_isHandoffInProgress) return;

            var newVm = _navigationStore.CurrentViewModel as ViewModelBase;
            
            // FIREWALL: If the new ViewModel is not an Auth-view, ignore it.
            // This stops the Dashboard from appearing in the Auth Window for a split second during handoff.
            if (IsAuthView(newVm))
            {
                CurrentView = newVm;
            }
            else
            {
                Serilog.Log.Debug("[AuthFirewall] Blocked non-auth ViewModel {VmName} from rendering in Auth Shell.", newVm?.GetType().Name ?? "null");
                // If we are already on an auth view, keep it. 
                // If we are transitioning to a black-hole (Dashboard resolution), just stay where we are.
            }
        }

        private bool IsAuthView(ViewModelBase? vm)
        {
            if (vm == null) return true; // Clearing the view is always allowed

            var type = vm.GetType();
            var ns = type.Namespace ?? string.Empty;
            var typeName = type.Name;
            
            // FIREWALL ALLOWLIST:
            // 1. Any ViewModel in the .Auth sub-namespace (SplashOnboarding, etc.)
            // 2. The main LoginViewModel
            // 3. The initial FacilityOnboarding or LicenseEntry views
            return ns.Contains("ViewModels.Auth") || 
                   typeName == "LoginViewModel" || 
                   typeName == "SplashOnboardingViewModel" ||
                   typeName == "FacilityOnboardingViewModel" ||
                   typeName == "OnboardingOwnerViewModel" ||
                   typeName == "OnboardingViewModel" ||
                   typeName == "LicenseEntryViewModel" ||
                   typeName == "ActivationChoiceViewModel" ||
                   typeName == "PreferencesSetupViewModel";
        }

        private void OnModalStorePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModalNavigationStore.CurrentModalViewModel) ||
                e.PropertyName == nameof(ModalNavigationStore.IsOpen))
            {
                CurrentModal = _modalNavigationStore.CurrentModalViewModel;
                IsModalOpen = _modalNavigationStore.IsOpen;
            }
        }

        public void Dispose()
        {
            _modalNavigationStore.PropertyChanged -= OnModalStorePropertyChanged;
            if (_navigationStore != null)
            {
                _navigationStore.CurrentViewModelChanged -= OnCurrentViewModelChanged;
                _navigationStore.BackStackChanged -= OnBackStackChanged;
            }
        }
    }
}
