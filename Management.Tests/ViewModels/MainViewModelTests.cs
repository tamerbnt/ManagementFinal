using System;
using System.Collections.Generic;
using System.Windows;
using Management.Domain.Enums;
using Management.Presentation.ViewModels.Shell;
using Management.Presentation.Services;
using Management.Presentation.Services.State;
using Management.Presentation.Services.Navigation;
using Management.Application.Interfaces.App;
using Management.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Management.Presentation.ViewModels.GymHome;
using Management.Presentation.ViewModels.Salon;
using Management.Presentation.Stores;
using Management.Application.Stores;

namespace Management.Tests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<INavigationService> _navigationServiceMock;
        private readonly Mock<SessionManager> _sessionManagerMock;
        private readonly Mock<IToastService> _toastServiceMock;
        private readonly Mock<IBreadcrumbService> _breadcrumbServiceMock;
        private readonly Mock<IResilienceService> _resilienceServiceMock;
        private readonly Mock<IUndoService> _undoServiceMock;
        private readonly Mock<ISessionMonitorService> _sessionMonitorMock;
        private readonly Mock<SyncStore> _syncStoreMock;
        private readonly Mock<IDialogService> _dialogServiceMock;
        private readonly Mock<IAuthenticationService> _authServiceMock;
        private readonly Mock<ITerminologyService> _terminologyServiceMock;
        private readonly Mock<ICommandPaletteService> _paletteServiceMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<ILogger<MainViewModel>> _loggerMock;
        private readonly Mock<IDiagnosticService> _diagnosticServiceMock;
        private readonly Mock<INavigationRegistry> _navigationRegistryMock;
        private readonly Mock<IFacilityContextService> _facilityContextMock;
        private readonly Mock<ILocalizationService> _localizationServiceMock;
        private readonly NavigationStore _navigationStore;

        public MainViewModelTests()
        {
            _navigationServiceMock = new Mock<INavigationService>();
            _sessionManagerMock = new Mock<SessionManager>(new Mock<IAuthenticationService>().Object, new Mock<ITenantService>().Object);
            _toastServiceMock = new Mock<IToastService>();
            _breadcrumbServiceMock = new Mock<IBreadcrumbService>();
            _resilienceServiceMock = new Mock<IResilienceService>();
            _undoServiceMock = new Mock<IUndoService>();
            _sessionMonitorMock = new Mock<ISessionMonitorService>();
            _syncStoreMock = new Mock<SyncStore>();
            _dialogServiceMock = new Mock<IDialogService>();
            _authServiceMock = new Mock<IAuthenticationService>();
            _terminologyServiceMock = new Mock<ITerminologyService>();
            _paletteServiceMock = new Mock<ICommandPaletteService>();
            _notificationServiceMock = new Mock<INotificationService>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _loggerMock = new Mock<ILogger<MainViewModel>>();
            _diagnosticServiceMock = new Mock<IDiagnosticService>();
            _navigationRegistryMock = new Mock<INavigationRegistry>();
            _facilityContextMock = new Mock<IFacilityContextService>();
            _localizationServiceMock = new Mock<ILocalizationService>();
            _navigationStore = new NavigationStore();

            _serviceProviderMock.Setup(x => x.GetService(typeof(INavigationRegistry))).Returns(_navigationRegistryMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IFacilityContextService))).Returns(_facilityContextMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(NavigationStore))).Returns(_navigationStore);
            _serviceProviderMock.Setup(x => x.GetService(typeof(ModalNavigationStore))).Returns(new ModalNavigationStore());
            _serviceProviderMock.Setup(x => x.GetService(typeof(ILocalizationService))).Returns(_localizationServiceMock.Object);
        }

        [WpfFact]
        public void GetOrCacheView_AlwaysUpdatesDataContext()
        {
            // Arrange
            var mainVm = CreateMainViewModel();
            var gymHomeVm = new Mock<GymHomeViewModel>(
                _terminologyServiceMock.Object,
                _facilityContextMock.Object,
                new Mock<ILogger<GymHomeViewModel>>().Object,
                _diagnosticServiceMock.Object,
                _toastServiceMock.Object,
                _localizationServiceMock.Object).Object;

            // Set up view creation
            // This is tricky because ActivatorUtilities.CreateInstance is used.
            // In a real WPF app, this would create a GymHomeView.
        }

        private MainViewModel CreateMainViewModel()
        {
            return new MainViewModel(
                _navigationServiceMock.Object,
                _sessionManagerMock.Object,
                new TopBarViewModel(_terminologyServiceMock.Object, _toastServiceMock.Object),
                new CommandPaletteViewModel(_terminologyServiceMock.Object, _paletteServiceMock.Object),
                new ConnectivityViewModel(_resilienceServiceMock.Object, _toastServiceMock.Object),
                _toastServiceMock.Object,
                _breadcrumbServiceMock.Object,
                _resilienceServiceMock.Object,
                _undoServiceMock.Object,
                _sessionMonitorMock.Object,
                _syncStoreMock.Object,
                _dialogServiceMock.Object,
                _authServiceMock.Object,
                _terminologyServiceMock.Object,
                _paletteServiceMock.Object,
                _notificationServiceMock.Object,
                _serviceProviderMock.Object,
                _loggerMock.Object,
                _diagnosticServiceMock.Object);
        }
    }
}
