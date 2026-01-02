using System;
using Management.Application.Stores;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;
using Management.Infrastructure.Services;
using Moq;
using Xunit;
using FluentAssertions;

namespace Management.Tests.Unit
{
    public class NavigationTests
    {
        private readonly Mock<INavigationService> _navServiceMock = new();
        private readonly Mock<IAuthenticationService> _authServiceMock = new();
        private readonly Mock<IResilienceService> _resilienceServiceMock = new Mock<IResilienceService>();
        private readonly Mock<IUndoService> _undoServiceMock = new();
        private readonly Mock<ISessionMonitorService> _sessionMonitorMock = new();
        private readonly Mock<IDialogService> _dialogServiceMock = new();
        private readonly Mock<IDispatcher> _dispatcherMock = new();
        private readonly Mock<IFacilityContextService> _facilityContextMock = new();
        private readonly Mock<ITerminologyService> _termServiceMock = new();
        private readonly Mock<ICommandPaletteService> _paletteServiceMock = new();
        private readonly Mock<INotificationService> _notificationServiceMock = new();

        public NavigationTests()
        {
            _dispatcherMock.Setup(d => d.Invoke(It.IsAny<Action>())).Callback<Action>(a => a());
        }

        [Fact]
        public void ToggleDensity_ShouldChangeRowHeight()
        {
            // Arrange
            var vm = CreateMainViewModel();
            vm.GlobalRowHeight.Should().Be(72);

            // Act
            vm.ToggleDensityCommand.Execute(null);

            // Assert
            vm.GlobalRowHeight.Should().Be(48);

            // Act again
            vm.ToggleDensityCommand.Execute(null);

            // Assert
            vm.GlobalRowHeight.Should().Be(72);
        }

        [Fact]
        public void Connectivity_WhenOffline_ShouldShowBanner()
        {
            // Arrange
            var vm = CreateMainViewModel();
            vm.ShowOfflineBanner.Should().BeFalse();

            // Act - Raise event on mock
            _resilienceServiceMock.Raise(r => r.ConnectivityChanged += null, null, false);

            // Assert
            vm.IsOffline.Should().BeTrue();
            vm.ShowOfflineBanner.Should().BeTrue();
        }

        [Fact]
        public void SelectionRule_WhenSearchQueryChanges_ShouldClearSelection()
        {
            // Arrange
            var navStore = new NavigationStore();
            
            // Mock MembersViewModel
            var membersServiceMock = new Mock<IMemberService>();
            var emailServiceMock = new Mock<IEmailService>();
            var membersVm = new MembersViewModel(
                membersServiceMock.Object,
                _navServiceMock.Object,
                _dialogServiceMock.Object,
                _facilityContextMock.Object,
                _notificationServiceMock.Object,
                emailServiceMock.Object
            );

            navStore.CurrentViewModel = membersVm;

            var vm = CreateMainViewModel(navStore);

            // Mock selection
            membersVm.FilteredMembers.Add(new MemberListItemViewModel(new Management.Domain.DTOs.MemberDto { FullName = "Test" }) { IsSelected = true });
            membersVm.SelectedCount = 1; // Manually sync for unit test (private RecalculateSelection not called automatically on list add)
            membersVm.SelectedCount.Should().Be(1);

            // Act
            vm.SearchQuery = "New Search";

            // Assert
            membersVm.SelectedCount.Should().Be(0);
        }

        private MainViewModel CreateMainViewModel(NavigationStore navStore = null)
        {
            return new MainViewModel(
                navigationStore: navStore ?? new NavigationStore(),
                navigationService: _navServiceMock.Object,
                authService: _authServiceMock.Object,
                resilienceService: _resilienceServiceMock.Object,
                undoService: _undoServiceMock.Object,
                sessionMonitor: _sessionMonitorMock.Object,
                syncStore: new SyncStore(),
                dialogService: _dialogServiceMock.Object,
                dispatcher: _dispatcherMock.Object,
                facilityContext: _facilityContextMock.Object,
                terminologyService: _termServiceMock.Object,
                commandPalette: _paletteServiceMock.Object,
                notificationService: _notificationServiceMock.Object
            );
        }
    }
}
