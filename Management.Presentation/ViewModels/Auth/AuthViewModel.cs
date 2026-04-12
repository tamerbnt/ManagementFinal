using CommunityToolkit.Mvvm.ComponentModel;
using Management.Presentation.Services;
using Management.Application.Stores;
using Management.Presentation.Stores;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Shared;
using System;

namespace Management.Presentation.ViewModels
{
    public partial class AuthViewModel : ViewModelBase, IDisposable
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly NavigationStore _navigationStore;
        private readonly INotificationService _notificationService;

        private ViewModelBase? _currentView;
        public ViewModelBase? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }
        
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

        public System.Collections.ObjectModel.ObservableCollection<ToastViewModel> ActiveToasts => _notificationService.ActiveToasts;

        public AuthViewModel(ModalNavigationStore modalNavigationStore, NavigationStore navigationStore, INotificationService notificationService)
        {
            _modalNavigationStore = modalNavigationStore;
            _modalNavigationStore.PropertyChanged += OnModalStorePropertyChanged;

            _navigationStore = navigationStore;
            _notificationService = notificationService;
            
            // ARCHITECTURE GUARD: Only allow Auth-related ViewModels in this shell.
            // This prevents the Dashboard from ever "Mashup" rendering inside the Auth card.
            var candidateVm = _navigationStore.CurrentViewModel as ViewModelBase;
            _currentView = IsAuthView(candidateVm) ? candidateVm : null;

            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;

            CurrentModal = _modalNavigationStore.CurrentModalViewModel;
            IsModalOpen = _modalNavigationStore.IsOpen;
        }

        private bool _isHandoffInProgress;

        public void PrepareForHandoff()
        {
            Serilog.Log.Information("[AuthViewModel] Handoff initiated. Disconnecting from NavigationStore and clearing view...");
            _isHandoffInProgress = true;
            _navigationStore.CurrentViewModelChanged -= OnCurrentViewModelChanged;
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
                   typeName == "LicenseEntryViewModel";
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
            }
        }
    }
}
