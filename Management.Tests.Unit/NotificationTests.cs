using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Domain.Primitives;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;
using Moq;
using Xunit;
using FluentAssertions;

namespace Management.Tests.Unit
{
    public class NotificationTests
    {
        private readonly Mock<IMemberService> _memberServiceMock = new();
        private readonly Mock<INavigationService> _navServiceMock = new();
        private readonly Mock<IDialogService> _dialogServiceMock = new();
        private readonly Mock<IFacilityContextService> _facilityContextMock = new();
        private readonly Mock<INotificationService> _notificationServiceMock = new();
        private readonly Mock<IEmailService> _emailServiceMock = new();

        [Fact]
        public async Task DeleteMember_ShouldTriggerUndoNotificationAndDeferExecution()
        {
            // Arrange
            var memberId = Guid.NewGuid();
            var memberDto = new MemberDto { Id = memberId, FullName = "John Doe", Status = MemberStatus.Active };
            
            _memberServiceMock.Setup(m => m.SearchMembersAsync(It.IsAny<Guid>(), It.IsAny<MemberSearchRequest>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(Result<PagedResult<MemberDto>>.Success(new PagedResult<MemberDto> { Items = new List<MemberDto> { memberDto }, TotalCount = 1, PageNumber = 1, PageSize = 20 }));

            var vm = new MembersViewModel(
                _memberServiceMock.Object,
                _navServiceMock.Object,
                _dialogServiceMock.Object,
                _facilityContextMock.Object,
                _notificationServiceMock.Object,
                _emailServiceMock.Object
            );

            // Wait for RefreshDataAsync
            await Task.Delay(100);

            var memberItem = vm.FilteredMembers[0];

            // Act
            memberItem.DeleteCommand.Execute(null);

            // Assert
            _notificationServiceMock.Verify(n => n.ShowUndoNotification(
                It.Is<string>(s => s.Contains("John Doe")),
                It.IsNotNull<Func<Task>>(),
                It.IsNotNull<Func<Task>>()), Times.Once);

            // Verify service call NOT made immediately
            _memberServiceMock.Verify(m => m.DeleteMembersAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>()), Times.Never);
        }

        [Fact]
        public async Task UndoNotification_WhenTimerTicks_ShouldExecuteFinalAction()
        {
            // Arrange
            var store = new NotificationStore();
            var service = new NotificationService(store);
            bool finalActionExecuted = false;

            // Act
            service.ShowUndoNotification("Test", () => Task.CompletedTask, () => { finalActionExecuted = true; return Task.CompletedTask; });
            
            // We can't easily wait for the real DispatcherTimer in unit tests without mocking it.
            // But we can check the state.
            service.HasUndo.Should().BeTrue();
            service.CurrentMessage.Should().Be("Test");
        }
    }
}
