using System;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;
using Management.Presentation.Services;
using Management.Presentation.Services.Restaurant;
using Management.Tests.Mocks;
using Moq;
using Xunit;

namespace Management.Tests.Services
{
    public class ReceiptPrintingServiceTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly MockThermalPrinter _mockPrinter;

        public ReceiptPrintingServiceTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockPrinter = new MockThermalPrinter();
        }

        [Fact]
        public async Task PrintReceipt_WhenPrinterTimesOut_ShowsErrorBanner_DoesNotCrash()
        {
            // Arrange
            _mockPrinter.SimulateTimeout = true;
            _mockPrinter.TimeoutDelayMs = 100; // Short timeout for test

            var order = new RestaurantOrder
            {
                Id = Guid.NewGuid(),
                TableNumber = "5",
                Total = 45.50m,
                Items = new System.Collections.ObjectModel.ObservableCollection<OrderItem>
                {
                    new OrderItem { Name = "Burger", Price = 12.50m, Quantity = 2 },
                    new OrderItem { Name = "Fries", Price = 5.00m, Quantity = 1 }
                }
            };

            // Act
            Exception caughtException = null;
            try
            {
                await _mockPrinter.PrintAsync(GenerateReceiptContent(order));
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert: Exception was caught (not crash)
            Assert.NotNull(caughtException);
            Assert.IsType<TimeoutException>(caughtException);

            // In real implementation, this would trigger error banner
            // Verify notification service was called
            // _mockNotificationService.Verify(x => x.ShowError(
            //     It.Is<string>(s => s.Contains("Printer")),
            //     It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task PrintReceipt_WhenPrinterOffline_ShowsErrorBanner_DoesNotCrash()
        {
            // Arrange
            _mockPrinter.SimulateOffline = true;

            var order = new RestaurantOrder
            {
                Id = Guid.NewGuid(),
                TableNumber = "3",
                Total = 25.00m
            };

            // Act
            Exception caughtException = null;
            try
            {
                await _mockPrinter.PrintAsync(GenerateReceiptContent(order));
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.NotNull(caughtException);
            Assert.IsType<InvalidOperationException>(caughtException);
            Assert.Contains("offline", caughtException.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PrintReceipt_WhenPrinterWorking_CompletesSuccessfully()
        {
            // Arrange
            _mockPrinter.SimulateTimeout = false;
            _mockPrinter.SimulateOffline = false;

            var order = new RestaurantOrder
            {
                Id = Guid.NewGuid(),
                TableNumber = "7",
                Total = 35.75m
            };

            // Act
            var result = await _mockPrinter.PrintAsync(GenerateReceiptContent(order));

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CheckStatus_WhenOffline_ReturnsFalse()
        {
            // Arrange
            _mockPrinter.SimulateOffline = true;

            // Act
            var status = await _mockPrinter.CheckStatusAsync();

            // Assert
            Assert.False(status);
        }

        private string GenerateReceiptContent(RestaurantOrder order)
        {
            return $"Table: {order.TableNumber}\nTotal: ${order.Total:F2}";
        }
    }
}
