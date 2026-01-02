using FluentAssertions;
using Management.Infrastructure.Services;
using Xunit;

namespace Management.Tests.Unit.Infrastructure
{
    public class HardwareTests
    {
        [Fact]
        public void HardwareService_GetId_ShouldProduceSha256Hash()
        {
            // Arrange
            var service = new HardwareService();

            // Act
            var id = service.GetHardwareId();

            // Assert
            id.Should().NotBeNullOrEmpty();
            id.Length.Should().Be(64); // SHA256 in hex is 64 chars
            id.Should().MatchRegex("^[0-9A-F]+$"); // Hexadecimal check
        }

        [Fact]
        public void HardwareService_GetId_ShouldBeConsistent()
        {
            // Arrange
            var service = new HardwareService();

            // Act
            var id1 = service.GetHardwareId();
            var id2 = service.GetHardwareId();

            // Assert
            id1.Should().Be(id2, "Fingerprint must be the same on the same machine.");
        }
    }
}
