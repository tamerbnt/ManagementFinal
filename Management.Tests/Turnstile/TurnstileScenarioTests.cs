using FluentAssertions;
using Management.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Management.Tests.Turnstile
{
    // ─── SCENARIO TESTS ──────────────────────────────────────────────────────────

    public class TurnstileScenarioTests : IAsyncLifetime
    {
        private readonly TurnstileTestFixture _fixture = new();

        public async Task InitializeAsync() => await _fixture.StartWorkerAsync();
        public Task DisposeAsync() { _fixture.Dispose(); return Task.CompletedTask; }

        /// <summary>
        /// SCENARIO C1 — Rush Hour Load Test
        /// 20 distinct members scan over a short period.
        /// All should be processed, logged to DB, no crashes.
        /// </summary>
        [Fact]
        public async Task RushHour_20MembersIn60Seconds_AllShouldBeProcessed()
        {
            // Arrange: seed 20 unique active members
            var members = new List<(string cardId, Member member)>();
            for (int i = 1; i <= 20; i++)
            {
                var cardId = $"RUSH-CARD-{i:D3}";
                var member = _fixture.SeedActiveMember(cardId, $"Member {i}");
                members.Add((cardId, member));
            }

            // Act: stagger scans 50ms apart (total ~1s, not 60s for test speed)
            // In production this simulates 20 scans over 60s — here we use 50ms
            // intervals since it's all in-memory and we're purely testing correctness.
            foreach (var (cardId, _) in members)
            {
                _fixture.Mock.SimulatePhysicalScan(cardId);
                await Task.Delay(50); // slight stagger to mimic real-world timing
            }

            // Give the async processing queue time to drain
            await Task.Delay(2000);

            // Assert: All 20 have an AccessEvent logged
            var events = _fixture.AccessEventLog;

            foreach (var (cardId, _) in members)
            {
                events.Should().Contain(e => e.CardId == cardId,
                    $"AccessEvent for {cardId} should be logged");

                events.Where(e => e.CardId == cardId)
                    .Should().ContainSingle(e => e.IsAccessGranted == true,
                        $"member {cardId} should be granted access");
            }

            // Assert: Gate opened 20 times (once per valid member)
            _fixture.Mock.GateOpenedCount.Should().Be(20,
                "exactly 20 gate open operations should be triggered for 20 valid members");
        }

        /// <summary>
        /// SCENARIO C2 — Connection Drop During Scan
        /// The gate hardware disconnects during an active scan.
        /// Business logic (event logging) should still succeed.
        /// The system should survive without crashing.
        /// </summary>
        [Fact]
        public async Task ConnectionDrop_DuringScan_ShouldLogEventButNotCrash()
        {
            // Arrange
            _fixture.SeedActiveMember("CARD-CAROL", "Carol");

            // Configure mock to drop connection when OpenGateAsync is called
            _fixture.Mock.DropConnectionOnNextOpen = true;

            // Act
            _fixture.Mock.SimulatePhysicalScan("CARD-CAROL");
            await Task.Delay(500);

            // Assert: AccessEvent was still logged (business logic completed
            // before the gate-open IOException propagated to the catch block)
            var events = _fixture.AccessEventLog;
            events.Should().NotBeEmpty("an AccessEvent must be logged even if the gate hardware fails");

            // Assert: Application is still alive (worker didn't crash)
            // Verify by triggering another scan after "reconnect"
            _fixture.Mock.IsConnected = true; // simulate reconnect
            _fixture.SeedActiveMember("CARD-CAROL-2", "Carol2");

            _fixture.Mock.SimulatePhysicalScan("CARD-CAROL-2");
            await Task.Delay(500);

            _fixture.Mock.GateOpenedCount.Should().BeGreaterThanOrEqualTo(1,
                "gate should open normally for the next valid scan after reconnect");
        }
    }
}
