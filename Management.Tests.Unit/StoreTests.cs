using FluentAssertions;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Services;
using Moq;
using Xunit;

namespace Management.Tests.Unit.Application
{
    public class StoreTests
    {
        [Fact]
        public void NavigationStore_ShouldTriggerEvent_WhenViewModelChanges()
        {
            // Arrange
            var store = new NavigationStore();
            bool eventTriggered = false;
            store.CurrentViewModelChanged += () => eventTriggered = true;

            // Act
            store.CurrentViewModel = new object();

            // Assert
            eventTriggered.Should().BeTrue("CurrentViewModelChanged event must be triggered on update.");
        }

        [Fact]
        public void AccountStore_Login_ShouldSetStateAndTenant()
        {
            // Arrange
            var tenantServiceMock = new Mock<ITenantService>();
            var store = new AccountStore(tenantServiceMock.Object);
            var user = new StaffDto { FullName = "Admin User", TenantId = Guid.NewGuid() };

            // Act
            store.Login(user);

            // Assert
            store.IsLoggedIn.Should().BeTrue();
            store.CurrentAccount.Should().Be(user);
            tenantServiceMock.Verify(s => s.SetTenantId(user.TenantId), Times.Once);
        }
    }
}
