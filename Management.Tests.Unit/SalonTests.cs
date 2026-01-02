using Bogus;
using FluentAssertions;
using Management.Domain.Models.Salon;
using Xunit;

namespace Management.Tests.Unit
{
    public class SalonTests
    {
        private readonly Faker<Appointment> _appointmentFaker;

        public SalonTests()
        {
            var staffId = Guid.NewGuid();
            _appointmentFaker = new Faker<Appointment>()
                .RuleFor(a => a.Id, f => f.Random.Guid())
                .RuleFor(a => a.StaffId, staffId)
                .RuleFor(a => a.StartTime, f => f.Date.Soon())
                .RuleFor(a => a.EndTime, (f, a) => a.StartTime.AddMinutes(30));
        }

        [Theory]
        [InlineData(10, 40, true)]  // Scenario A: Partial Overlap (B starts during A)
        [InlineData(5, 25, true)]   // Scenario B: Contained (B is inside A)
        [InlineData(30, 60, false)] // Scenario C: Sequential/Touching (B starts when A ends)
        [InlineData(35, 65, false)] // Scenario D: Gap (5 minute gap)
        public void ConflictsWith_Scenarios_ShouldMatchExpected(int bStartOffset, int bEndOffset, bool expectedConflict)
        {
            // Arrange
            var baseTime = DateTime.Today.AddHours(10);
            var appA = _appointmentFaker.Generate();
            appA.StartTime = baseTime;
            appA.EndTime = baseTime.AddMinutes(30);

            var appB = _appointmentFaker.Generate();
            appB.StartTime = baseTime.AddMinutes(bStartOffset);
            appB.EndTime = baseTime.AddMinutes(bEndOffset);

            // Act
            var result = appA.ConflictsWith(appB);

            // Assert
            result.Should().Be(expectedConflict, 
                $"Conflict detection failed for Scenario: A({appA.StartTime:HH:mm}-{appA.EndTime:HH:mm}) vs B({appB.StartTime:HH:mm}-{appB.EndTime:HH:mm})");
        }

        [Fact]
        public void ConflictsWith_DifferentStaff_ShouldAlwaysBeFalse()
        {
            // Arrange
            var baseTime = DateTime.Today.AddHours(10);
            var appA = _appointmentFaker.Generate();
            appA.StaffId = Guid.NewGuid();
            
            var appB = _appointmentFaker.Generate();
            appB.StaffId = Guid.NewGuid(); // Different staff

            appA.StartTime = baseTime;
            appA.EndTime = baseTime.AddMinutes(60);
            appB.StartTime = baseTime.AddMinutes(10);
            appB.EndTime = baseTime.AddMinutes(40);

            // Act
            var result = appA.ConflictsWith(appB);

            // Assert
            result.Should().BeFalse("Appointments for different staff members should never conflict.");
        }
    }
}
