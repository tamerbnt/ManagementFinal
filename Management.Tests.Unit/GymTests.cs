using Bogus;
using FluentAssertions;
using Management.Domain.Models;
using Management.Domain.Enums;
using Xunit;

namespace Management.Tests.Unit
{
    public class GymTests
    {
        private readonly Faker<Member> _memberFaker;

        public GymTests()
        {
            _memberFaker = new Faker<Member>()
                .CustomInstantiator(f => Member.Register(
                    f.Name.FullName(),
                    Management.Domain.ValueObjects.Email.Create(f.Internet.Email()).Value,
                    Management.Domain.ValueObjects.PhoneNumber.Create("+1234567890").Value,
                    f.Random.Guid().ToString(),
                    f.Random.Guid()
                ).Value);
        }

        [Fact]
        public void Member_WhenSubscriptionEndDateIsPast_IsActiveShouldBeFalse()
        {
            // Arrange
            var member = _memberFaker.Generate();
            member.ActivateMembership(DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(-1));

            // Act & Assert
            member.IsActive.Should().BeFalse("Member with expired subscription should not be active.");
        }

        [Fact]
        public void CanGrantAccess_WhenMemberIsExpired_ShouldReturnFalse()
        {
            // Arrange
            var member = _memberFaker.Generate();
            member.ActivateMembership(DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(-1));

            // Act
            var canAccess = member.CanGrantAccess();

            // Assert
            canAccess.Should().BeFalse("Access should be denied for expired memberships.");
        }

        [Fact]
        public void CanGrantAccess_WhenMemberIsActive_ShouldReturnTrue()
        {
            // Arrange
            var member = _memberFaker.Generate();
            member.ActivateMembership(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30));

            // Act
            var canAccess = member.CanGrantAccess();

            // Assert
            canAccess.Should().BeTrue("Access should be granted for active memberships.");
        }
    }
}
