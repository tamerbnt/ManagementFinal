using System;
using System.Linq;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Salon;
using Management.Infrastructure.Services;
using Moq;
using Xunit;
using FluentAssertions;

namespace Management.Tests.Unit
{
    public class SearchTests
    {
        private readonly Mock<INavigationService> _navServiceMock = new();
        private readonly Mock<IModalNavigationService> _modalServiceMock = new();
        private readonly Mock<IFacilityContextService> _facilityServiceMock = new();
        private readonly Mock<ISalonService> _salonServiceMock = new();

        [Fact]
        public void TogglePalette_ShouldFlipVisibility()
        {
            // Arrange
            var service = new CommandPaletteService(
                _navServiceMock.Object,
                _modalServiceMock.Object,
                _facilityServiceMock.Object,
                _salonServiceMock.Object
            );

            service.IsVisible = false;

            // Act
            service.TogglePaletteCommand.Execute(null);

            // Assert
            service.IsVisible.Should().BeTrue();

            // Act second time
            service.TogglePaletteCommand.Execute(null);

            // Assert
            service.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void FuzzySearch_WhenRestaurant_ShouldShowNewOrder()
        {
            // Arrange
            _facilityServiceMock.Setup(f => f.CurrentFacility).Returns(FacilityType.Restaurant);
            var service = new CommandPaletteService(
                _navServiceMock.Object,
                _modalServiceMock.Object,
                _facilityServiceMock.Object,
                _salonServiceMock.Object
            );

            // Act
            service.SearchQuery = "New";

            // Assert
            service.Results.Should().Contain(c => c.Label == "New Order");
            service.Results.Should().NotContain(c => c.Label == "New Member");
        }

        [Fact]
        public void FuzzySearch_WhenGym_ShouldShowNewMember()
        {
            // Arrange
            _facilityServiceMock.Setup(f => f.CurrentFacility).Returns(FacilityType.Gym);
            var service = new CommandPaletteService(
                _navServiceMock.Object,
                _modalServiceMock.Object,
                _facilityServiceMock.Object,
                _salonServiceMock.Object
            );

            // Act
            service.SearchQuery = "New";

            // Assert
            service.Results.Should().Contain(c => c.Label == "New Member");
            service.Results.Should().NotContain(c => c.Label == "New Order");
        }
    }
}
