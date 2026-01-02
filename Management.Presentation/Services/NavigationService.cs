using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Management.Application.Stores;
using Management.Presentation.ViewModels;
using Management.Presentation.Extensions;

namespace Management.Presentation.Services
{
    public class NavigationService : INavigationService
    {
        private readonly NavigationStore _navigationStore;
        private readonly Func<Type, ViewModelBase> _viewModelFactory;
        private readonly IDispatcher _dispatcher;
        private readonly ILogger<NavigationService>? _logger;

        // Map indices to Types for the sidebar (Legacy support)
        // Order must match MainViewModel.InitializeNavigationItems
        private readonly List<Type> _sidebarMap = new()
        {
            typeof(DashboardViewModel),      // 0
            typeof(AccessControlViewModel),  // 1
            typeof(MembersViewModel),        // 2
            typeof(RegistrationsViewModel),  // 3
            typeof(HistoryViewModel),        // 4
            typeof(FinanceAndStaffViewModel),// 5
            typeof(ShopViewModel),           // 6
            typeof(SettingsViewModel)        // 7
        };

        public NavigationService(
            NavigationStore navigationStore,
            Func<Type, ViewModelBase> viewModelFactory,
            IDispatcher dispatcher,
            ILogger<NavigationService>? logger = null)
        {
            _navigationStore = navigationStore;
            _viewModelFactory = viewModelFactory;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public async Task NavigateToAsync(int index)
        {
            if (index < 0 || index >= _sidebarMap.Count)
            {
                _logger?.LogWarning("Invalid navigation index: {Index}", index);
                return;
            }

            var type = _sidebarMap[index];
            await NavigateInternalAsync(type);
        }

        public async Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase
        {
            await NavigateInternalAsync(typeof(TViewModel));
        }

        public async Task NavigateToLoginAsync()
        {
             await NavigateInternalAsync(typeof(LoginViewModel));
        }

        private async Task NavigateInternalAsync(Type viewModelType)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _logger?.LogInformation("Navigating to {ViewModelType}", viewModelType.Name);
                    var viewModel = _viewModelFactory(viewModelType);
                    _navigationStore.CurrentViewModel = viewModel;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to navigate to {ViewModelType}", viewModelType.Name);
                    // Optionally show error dialog here
                    throw; 
                }
            });
        }
    }
}
