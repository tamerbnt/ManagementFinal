using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Management.Presentation.Services.State;
using Management.Application.Stores;
using Management.Presentation.ViewModels;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.ViewModels.AccessControl;
using Management.Presentation.ViewModels.Members;
using Management.Presentation.ViewModels.Registrations;
using Management.Presentation.ViewModels.History;
using Management.Presentation.ViewModels.Finance;
using Management.Presentation.ViewModels.Shop;
using Management.Presentation.ViewModels.Settings;
using Management.Presentation.ViewModels.GymHome;
using Management.Application.Interfaces.ViewModels;

using Management.Presentation.Services.Navigation;
using System.Linq;

namespace Management.Presentation.Services
{
    public class NavigationService : INavigationService
    {
        private readonly NavigationStore _navigationStore;
        private readonly Func<Type, ViewModelBase> _viewModelFactory;
        private readonly IDispatcher _dispatcher;
        private readonly ILogger<NavigationService>? _logger;
        private readonly Management.Application.Interfaces.App.IToastService _toastService;
        private readonly INavigationRegistry _navigationRegistry;
        private readonly SessionManager _sessionManager;

        public NavigationService(
            NavigationStore navigationStore,
            Func<Type, ViewModelBase> viewModelFactory,
            IDispatcher dispatcher,
            Management.Application.Interfaces.App.IToastService toastService,
            INavigationRegistry navigationRegistry,
            SessionManager sessionManager,
            ILogger<NavigationService>? logger = null)
        {
            _navigationStore = navigationStore;
            _viewModelFactory = viewModelFactory;
            _dispatcher = dispatcher;
            _toastService = toastService;
            _navigationRegistry = navigationRegistry;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task NavigateToHomeAsync()
        {
            var facility = _sessionManager.CurrentFacility;
            var homeType = _navigationRegistry.GetHomeViewType(facility);
            
            if (homeType == null)
            {
                _logger?.LogWarning("No home view type registered for facility: {Facility}. Falling back to default Dashboard.", facility);
                homeType = typeof(DashboardViewModel);
            }

            await NavigateInternalAsync(homeType);
        }

        public async Task NavigateToAsync(int index)
        {
            var facility = _sessionManager.CurrentFacility;
            var items = _navigationRegistry.GetItems(facility).OrderBy(i => i.Order).ToList();

            if (index < 0 || index >= items.Count)
            {
                _logger?.LogWarning("Invalid navigation index {Index} for facility {Facility}", index, facility);
                return;
            }

            var type = items[index].TargetViewModelType;
            await NavigateInternalAsync(type);
        }

        public async Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase
        {
            await NavigateInternalAsync(typeof(TViewModel));
        }

        public async Task NavigateToAsync<TViewModel>(object parameter) where TViewModel : ViewModelBase
        {
            await NavigateInternalAsync(typeof(TViewModel), parameter);
        }

        public async Task NavigateToLoginAsync()
        {
             await NavigateInternalAsync(typeof(LoginViewModel));
        }

        private async Task NavigateInternalAsync(Type viewModelType, object? parameter = null)
        {
            await _dispatcher.InvokeAsync(async () => await ExecuteNavigation(viewModelType, parameter));
        }

        private readonly Dictionary<Type, object> _viewCache = new();

        private async Task ExecuteNavigation(Type viewModelType, object? parameter)
        {
            // Yield to the UI thread to allow high-priority rendering (like window corners/chrome) 
            // to process before any potentially heavy ViewModel initialization starts.
            await Task.Yield();

            try
            {
                _logger?.LogInformation("Navigating to {ViewModelType} with parameter {Parameter}", viewModelType.Name, parameter);
                
                // 1. Phased Construction
                _navigationStore.IsNavigating = true;
                var viewModel = _viewModelFactory(viewModelType);
                _navigationStore.NextViewModel = viewModel;

                // 2. Pre-Initialization (Lightweight)
                if (viewModel is INavigationalLifecycle lifecycleVm)
                {
                    await lifecycleVm.PreInitializeAsync();
                }

                if (parameter != null && viewModel is IParameterReceiver receiver)
                {
                    await receiver.SetParameterAsync(parameter);
                }

                // 3. Visual Swap (Triggers WPF DataTemplate switch)
                _navigationStore.CurrentViewModel = viewModel;

                // 4. Deferred Loading (Wait for UI Transition & Collection Population)
                if (viewModel is INavigationalLifecycle deferredVm)
                {
                    _logger?.LogInformation("Triggering Deferred Loading for {ViewModelType}", viewModelType.Name);
                    await deferredVm.LoadDeferredAsync();
                }
                else if (viewModel is IAsyncViewModel asyncVm)
                {
                    // Fallback for legacy 1-phase async ViewModels
                    _ = Task.Run(async () =>
                    {
                        try { await asyncVm.InitializeAsync(); }
                        catch (Exception ex) { _logger?.LogError(ex, "Failed to initialize legacy AsyncViewModel"); }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to {ViewModelType}", viewModelType.Name);
                _toastService.ShowError($"Navigation failure: {ex.Message}");
                _navigationStore.IsNavigating = false;
                throw; 
            }
        }
    }
}
