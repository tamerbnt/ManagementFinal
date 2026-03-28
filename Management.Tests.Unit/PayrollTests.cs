using FluentAssertions;
using Management.Application.Features.Finance.Commands.CreatePayrollEntry;
using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Management.Application.Notifications;

namespace Management.Tests.Unit.Application
{
    public class PayrollTests
    {
        private readonly Mock<IPayrollRepository> _payrollRepoMock;
        private readonly Mock<IStaffRepository> _staffRepoMock;
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
        private readonly Mock<IMediator> _mediatorMock;
        private readonly Mock<ILogger<CreatePayrollEntryCommandHandler>> _loggerMock;

        public PayrollTests()
        {
            _payrollRepoMock = new Mock<IPayrollRepository>();
            _staffRepoMock = new Mock<IStaffRepository>();
            _uowMock = new Mock<IUnitOfWork>();
            _transactionMock = new Mock<IUnitOfWorkTransaction>();
            _mediatorMock = new Mock<IMediator>();
            _loggerMock = new Mock<ILogger<CreatePayrollEntryCommandHandler>>();

            _uowMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_transactionMock.Object);
        }

        [Fact]
        public async Task CreatePayrollEntryHandler_ShouldMarkAsPaid_WhenIsPaidIsTrue()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var facilityId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            
            var staff = StaffMember.Recruit(
                tenantId, 
                facilityId, 
                "John Doe", 
                Email.Create("john@example.com").Value, 
                PhoneNumber.Create("+1234567890").Value, 
                StaffRole.Staff, 
                5000, 
                1).Value;

            _staffRepoMock.Setup(r => r.GetByIdAsync(staffId, It.IsAny<Guid?>())).ReturnsAsync(staff);

            var handler = new CreatePayrollEntryCommandHandler(
                _payrollRepoMock.Object, 
                _staffRepoMock.Object, 
                _uowMock.Object, 
                _mediatorMock.Object, 
                _loggerMock.Object);

            var dto = new PayrollEntryDto
            {
                StaffId = staffId,
                Amount = 5000,
                PayPeriodStart = DateTime.Today.AddDays(-30),
                PayPeriodEnd = DateTime.Today
            };

            var command = new CreatePayrollEntryCommand(dto, IsPaid: true);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            
            // Verify that the entry added to the repository HAS the correct PaidAmount
            // We use It.IsAny<bool>() for the saveChanges parameter to avoid optional argument issues in expression trees
            _payrollRepoMock.Verify(r => r.AddAsync(
                It.Is<PayrollEntry>(e => e.PaidAmount.Amount == 5000), 
                It.IsAny<bool>()), Times.Once);

            // Verify transaction sequence
            _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreatePayrollEntryHandler_ShouldNotMarkAsPaid_WhenIsPaidIsFalse()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var staff = StaffMember.Recruit(
                Guid.NewGuid(), 
                Guid.NewGuid(), 
                "John Doe", 
                Email.Create("john@example.com").Value, 
                PhoneNumber.Create("+1234567890").Value, 
                StaffRole.Staff, 
                5000, 
                1).Value;

            _staffRepoMock.Setup(r => r.GetByIdAsync(staffId, It.IsAny<Guid?>())).ReturnsAsync(staff);

            var handler = new CreatePayrollEntryCommandHandler(
                _payrollRepoMock.Object, 
                _staffRepoMock.Object, 
                _uowMock.Object, 
                _mediatorMock.Object, 
                _loggerMock.Object);

            var dto = new PayrollEntryDto
            {
                StaffId = staffId,
                Amount = 5000,
                PayPeriodStart = DateTime.Today.AddDays(-30),
                PayPeriodEnd = DateTime.Today
            };

            var command = new CreatePayrollEntryCommand(dto, IsPaid: false);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            
            // Verify that the entry added to the repository HAS zero PaidAmount
            _payrollRepoMock.Verify(r => r.AddAsync(
                It.Is<PayrollEntry>(e => e.PaidAmount.Amount == 0), 
                It.IsAny<bool>()), Times.Once);
        }
    }
}
