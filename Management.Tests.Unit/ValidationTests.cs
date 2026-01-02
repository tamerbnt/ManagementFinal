using FluentValidation.TestHelper;
using Management.Application.Features.Members.Commands.CreateMember;
using Management.Application.Features.Restaurant.Commands.ProcessOrder;
using Management.Application.Features.Salon.Commands.BookAppointment;
using Management.Domain.DTOs;
using System;
using System.Collections.Generic;
using Xunit;

namespace Management.Tests.Unit.Application
{
    public class ValidationTests
    {
        private readonly CreateMemberCommandValidator _memberValidator;
        private readonly BookAppointmentValidator _salonValidator;
        private readonly ProcessOrderValidator _restaurantValidator;

        public ValidationTests()
        {
            _memberValidator = new CreateMemberCommandValidator();
            _salonValidator = new BookAppointmentValidator();
            _restaurantValidator = new ProcessOrderValidator();
        }

        [Fact]
        public void CreateMemberValidator_ShouldFail_WhenFullNameIsEmpty()
        {
            var command = new CreateMemberCommand(new MemberDto { FullName = "" });
            var result = _memberValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Member.FullName);
        }

        [Fact]
        public void CreateMemberValidator_ShouldFail_WhenEmailIsInvalid()
        {
            var command = new CreateMemberCommand(new MemberDto { Email = "invalid-email" });
            var result = _memberValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Member.Email);
        }

        [Fact]
        public void BookAppointmentValidator_ShouldFail_WhenDurationIsZero()
        {
            var command = new BookAppointmentCommand
            {
                StartTime = DateTime.Today.AddHours(10),
                EndTime = DateTime.Today.AddHours(10) // Zero duration
            };
            var result = _salonValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x).WithErrorMessage("Appointment duration cannot be zero.");
        }

        [Fact]
        public void ProcessOrderValidator_ShouldFail_WhenItemsAreEmpty()
        {
            var command = new ProcessOrderCommand
            {
                TableNumber = "T1",
                Items = new List<OrderItemDto>() // Empty list
            };
            var result = _restaurantValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Items).WithErrorMessage("Order must contain at least one item.");
        }

        [Fact]
        public void ProcessOrderValidator_ShouldFail_WhenItemPriceIsNegative()
        {
            var command = new ProcessOrderCommand
            {
                TableNumber = "T1",
                Items = new List<OrderItemDto> { new OrderItemDto { Name = "Burger", Price = -5.0m, Quantity = 1 } }
            };
            var result = _restaurantValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor("Items[0].Price");
        }
    }
}
