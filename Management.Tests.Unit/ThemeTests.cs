using System;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Services;
using Management.Application.Services;
using Management.Presentation.ViewModels;
using Management.Application.Services;
using Management.Infrastructure.Services;
using Management.Application.Services;
using Moq;
using Management.Application.Services;
using Xunit;
using Management.Application.Services;
using FluentAssertions;
using Management.Application.Services;

namespace Management.Tests.Unit
{
    public class ThemeTests
    {
        private readonly Mock<INavigationService> _navServiceMock = new();
        private readonly Mock<IAuthenticationService> _authServiceMock = new();
        private readonly Mock<IResilienceService> _resilienceServiceMock = new();
        private readonly Mock<IUndoService> _undoServiceMock = new();
        private readonly Mock<ISessionMonitorService> _sessionMonitorMock = new();
        private readonly Mock<IDialogService> _dialogServiceMock = new();
        private readonly Mock<IDispatcher> _dispatcherMock = new();
        private readonly Mock<IFacilityContextService> _facilityContextMock = new();
        private readonly Mock<ITerminologyService> _termServiceMock = new();
        private readonly Mock<ICommandPaletteService> _paletteServiceMock = new();
        private readonly Mock<INotificationService> _notificationServiceMock = new();

        [Fact]
        public void PersonalitySwap_WhenSalon_ShouldApplySalonBranding()
        {
            // Arrange
            _facilityContextMock.Setup(f => f.CurrentFacility).Returns(FacilityType.Salon);
            _termServiceMock.Setup(t => t.GetTerm("Guest")).Returns("Client");
            
            var navStore = new NavigationStore();
            var syncStore = new SyncStore();

            var vm = new MainViewModel(
                navStore,
                _navServiceMock.Object,
                _authServiceMock.Object,
                _resilienceServiceMock.Object,
                _undoServiceMock.Object,
                _sessionMonitorMock.Object,
                syncStore,
                _dialogServiceMock.Object,
                _dispatcherMock.Object,
                _facilityContextMock.Object,
                _termServiceMock.Object,
                _paletteServiceMock.Object,
                _notificationServiceMock.Object
            );

            // Assert
            vm.CurrentTerminology.Should().Be("Client");
            vm.ActiveThemePath.Should().Contain("Theme.Salon.xaml");
            vm.ActiveAccentBrush.Should().Be("#D8A7D8"); // Lavender Rose
        }

        [Fact]
        public void PersonalitySwap_WhenRestaurant_ShouldApplyRestaurantBranding()
        {
            // Arrange
            _facilityContextMock.Setup(f => f.CurrentFacility).Returns(FacilityType.Restaurant);
            _termServiceMock.Setup(t => t.GetTerm("Guest")).Returns("Customer");
            
            var navStore = new NavigationStore();
            var syncStore = new SyncStore();

            var vm = new MainViewModel(
                navStore,
                _navServiceMock.Object,
                _authServiceMock.Object,
                _resilienceServiceMock.Object,
                _undoServiceMock.Object,
                _sessionMonitorMock.Object,
                syncStore,
                _dialogServiceMock.Object,
                _dispatcherMock.Object,
                _facilityContextMock.Object,
                _termServiceMock.Object,
                _paletteServiceMock.Object,
                _notificationServiceMock.Object
            );

            // Assert
            vm.CurrentTerminology.Should().Be("Customer");
            vm.ActiveAccentBrush.Should().Be("#FF7F50"); // Coral
        }
    }
}
