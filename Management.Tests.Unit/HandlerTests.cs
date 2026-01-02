using Bogus;
using FluentAssertions;
using Management.Application.Features.Members.Commands.CreateMember;
using Management.Application.Features.Salon.Commands.BookAppointment;
using Management.Application.Stores;
using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Management.Tests.Unit.Application
{
    public class HandlerTests
    {
        private readonly Mock<IMemberRepository> _memberRepoMock;
        private readonly Mock<ITenantService> _tenantServiceMock;
        private readonly MemberStore _memberStore;
        private readonly Guid _testTenantId = Guid.NewGuid();

        public HandlerTests()
        {
            _memberRepoMock = new Mock<IMemberRepository>();
            _tenantServiceMock = new Mock<ITenantService>();
            _memberStore = new MemberStore();
            
            _tenantServiceMock.Setup(s => s.GetTenantId()).Returns(_testTenantId);
        }

        [Fact]
        public async Task CreateMemberHandler_ShouldInjectTenantId_BeforeSaving()
        {
            // Arrange
            var handler = new CreateMemberCommandHandler(_memberRepoMock.Object, _memberStore, _tenantServiceMock.Object);
            var memberDto = new Faker<MemberDto>()
                .RuleFor(m => m.FullName, f => f.Name.FullName())
                .RuleFor(m => m.Email, f => f.Internet.Email())
                .RuleFor(m => m.PhoneNumber, f => "+1234567890")
                .Generate();

            var command = new CreateMemberCommand(memberDto);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _memberRepoMock.Verify(r => r.AddAsync(It.Is<Member>(m => m.TenantId == _testTenantId)), Times.Once);
        }

        [Fact]
        public async Task BookAppointmentHandler_ShouldReturnSuccess_WhenNoConflicts()
        {
            // Arrange
            var salonRepoMock = new Mock<IReservationRepository>();
            var handler = new BookAppointmentCommandHandler(salonRepoMock.Object, _tenantServiceMock.Object);
            
            var command = new BookAppointmentCommand
            {
                ClientId = Guid.NewGuid(),
                StaffId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2)
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeEmpty();
        }

        [Fact]
        public async Task BookAppointmentHandler_ShouldReturnFailure_WhenConflictExists()
        {
            // Arrange
            var salonRepoMock = new Mock<IReservationRepository>();
            var handler = new BookAppointmentCommandHandler(salonRepoMock.Object, _tenantServiceMock.Object);

            var baseTime = DateTime.Today.AddHours(14);
            var staffId = Guid.NewGuid();
            
            // Create a conflicting reservation in the same tenant
            var existingReservation = Reservation.Book(
                Guid.NewGuid(), 
                Guid.NewGuid(), 
                "Salon", 
                baseTime, 
                baseTime.AddHours(1)).Value;
            
            existingReservation.TenantId = _testTenantId;

            var list = new System.Collections.Generic.List<Reservation> { existingReservation };
            salonRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            var command = new BookAppointmentCommand
            {
                StaffId = staffId, 
                StartTime = baseTime.AddMinutes(30), // Overlaps existingReservation
                EndTime = baseTime.AddHours(1.5)
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Salon.Conflict");
        }
    }
}
