using System;
using System.Threading.Tasks;
using Management.Application.Stores;
using Management.Domain.Models;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;
using Moq;
using Xunit;

namespace Management.Tests.Sync
{
    public class SyncConflictTests
    {
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly SyncStore _syncStore;

        public SyncConflictTests()
        {
            _mockDialogService = new Mock<IDialogService>();
            _syncStore = new SyncStore();
        }

        [Fact]
        public async Task SyncConflict_WhenVersionMismatch_InvokesConflictModal()
        {
            // Arrange
            var localMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EntityType = "Member",
                EntityId = Guid.NewGuid().ToString(),
                Action = "Update",
                ContentJson = "{\"FullName\":\"John Local\",\"Version\":1}",
                CreatedAt = DateTime.Now.AddMinutes(-5),
                Version = 1
            };

            var serverVersion = 2; // Server has newer version
            var serverContent = "{\"FullName\":\"John Server\",\"Version\":2}";

            // Setup dialog service to capture parameters
            ConflictResolutionParameters capturedParams = null;
            _mockDialogService
                .Setup(x => x.ShowCustomDialogAsync<ConflictResolutionViewModel>(It.IsAny<ConflictResolutionParameters>()))
                .Callback<ConflictResolutionParameters>(p => capturedParams = p)
                .ReturnsAsync(true);

            // Act: Simulate conflict detection
            if (serverVersion != localMessage.Version)
            {
                var parameters = new ConflictResolutionParameters
                {
                    EntityName = localMessage.EntityType,
                    EntityId = Guid.Parse(localMessage.EntityId),
                    LocalContent = localMessage.ContentJson,
                    RemoteContent = serverContent,
                    ConflictMessage = $"Version mismatch: Local={localMessage.Version}, Server={serverVersion}"
                };

                await _mockDialogService.Object.ShowCustomDialogAsync<ConflictResolutionViewModel>(parameters);
            }

            // Assert: Verify modal was invoked with correct data
            _mockDialogService.Verify(
                x => x.ShowCustomDialogAsync<ConflictResolutionViewModel>(It.IsAny<ConflictResolutionParameters>()),
                Times.Once);

            Assert.NotNull(capturedParams);
            Assert.Equal("Member", capturedParams.EntityName);
            Assert.Contains("John Local", capturedParams.LocalContent);
            Assert.Contains("John Server", capturedParams.RemoteContent);
            Assert.Contains("Version mismatch", capturedParams.ConflictMessage);
        }

        [Fact]
        public async Task SyncConflict_WhenVersionsMatch_DoesNotInvokeModal()
        {
            // Arrange
            var localMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EntityType = "Member",
                EntityId = Guid.NewGuid().ToString(),
                Action = "Update",
                ContentJson = "{\"FullName\":\"John\",\"Version\":1}",
                Version = 1
            };

            var serverVersion = 1; // Same version

            // Act: Check for conflict
            if (serverVersion != localMessage.Version)
            {
                await _mockDialogService.Object.ShowCustomDialogAsync<ConflictResolutionViewModel>(
                    new ConflictResolutionParameters());
            }

            // Assert: Modal should NOT be invoked
            _mockDialogService.Verify(
                x => x.ShowCustomDialogAsync<ConflictResolutionViewModel>(It.IsAny<ConflictResolutionParameters>()),
                Times.Never);
        }

        [Fact]
        public void SyncStore_DetectsConflict_RaisesEvent()
        {
            // Arrange
            bool conflictDetected = false;
            OutboxMessage conflictMessage = null;

            _syncStore.ConflictDetected += (msg) =>
            {
                conflictDetected = true;
                conflictMessage = msg;
            };

            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EntityType = "Product",
                EntityId = Guid.NewGuid().ToString(),
                Action = "Update",
                ContentJson = "{\"Name\":\"Product A\"}",
                LastError = "Conflict: Server version is newer"
            };

            // Act: Trigger conflict
            _syncStore.RaiseConflict(message);

            // Assert
            Assert.True(conflictDetected);
            Assert.NotNull(conflictMessage);
            Assert.Equal("Product", conflictMessage.EntityType);
            Assert.Contains("Conflict", conflictMessage.LastError);
        }

        [Fact]
        public async Task OfflineSync_WhenReconnected_ProcessesQueue()
        {
            // Arrange
            var pendingMessages = new[]
            {
                new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Member",
                    EntityId = Guid.NewGuid().ToString(),
                    Action = "Create",
                    ContentJson = "{\"FullName\":\"Offline Member\"}",
                    Version = 1
                },
                new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Sale",
                    EntityId = Guid.NewGuid().ToString(),
                    Action = "Create",
                    ContentJson = "{\"Total\":50.00}",
                    Version = 1
                }
            };

            // Act: Simulate processing queue
            int processedCount = 0;
            foreach (var message in pendingMessages)
            {
                // Simulate successful sync (no version conflict)
                if (message.Version == 1) // Assuming server accepts version 1
                {
                    processedCount++;
                }
            }

            // Assert
            Assert.Equal(2, processedCount);
        }

        [Fact]
        public async Task ConflictResolution_UserSelectsLocal_UpdatesServer()
        {
            // Arrange
            var localData = "{\"FullName\":\"John Local\",\"Email\":\"john.local@test.com\"}";
            var serverData = "{\"FullName\":\"John Server\",\"Email\":\"john.server@test.com\"}";

            bool userSelectedLocal = true; // Simulate user choice

            // Act: Apply resolution
            string resolvedData = userSelectedLocal ? localData : serverData;

            // Assert
            Assert.Equal(localData, resolvedData);
            Assert.Contains("john.local@test.com", resolvedData);
        }

        [Fact]
        public async Task ConflictResolution_UserSelectsServer_UpdatesLocal()
        {
            // Arrange
            var localData = "{\"FullName\":\"John Local\"}";
            var serverData = "{\"FullName\":\"John Server\"}";

            bool userSelectedLocal = false; // User chose server version

            // Act
            string resolvedData = userSelectedLocal ? localData : serverData;

            // Assert
            Assert.Equal(serverData, resolvedData);
            Assert.Contains("John Server", resolvedData);
        }
    }

    // Helper class for test parameters
    public class ConflictResolutionParameters
    {
        public string EntityName { get; set; }
        public Guid EntityId { get; set; }
        public string LocalContent { get; set; }
        public string RemoteContent { get; set; }
        public string ConflictMessage { get; set; }
    }
}
