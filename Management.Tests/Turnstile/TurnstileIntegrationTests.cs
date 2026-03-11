using FluentAssertions;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Management.Infrastructure.Configuration;
using Management.Infrastructure.Hardware;
using Management.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Management.Tests.Turnstile
{
    // ─── INTEGRATION TEST FIXTURE ────────────────────────────────────────────────

    /// <summary>
    /// Shared fixture that wires MockTurnstileService + AccessMonitoringWorker
    /// with mocked repositories and a real AccessControlService + AccessEventService.
    /// Uses Moq repos + an in-memory list for AccessEvent tracking.
    /// </summary>
    public class TurnstileTestFixture : IDisposable
    {
        public readonly Guid FacilityId = Guid.NewGuid();
        public readonly Guid TenantId = Guid.NewGuid();

        public readonly MockTurnstileService Mock;
        public readonly AccessMonitoringWorker Worker;

        // Persistent in-memory log of access events
        public readonly List<AccessEvent> AccessEventLog = new();

        // Mocked repositories — configure per test
        public readonly Mock<IMemberRepository> MemberRepo = new();
        public readonly Mock<IStaffRepository> StaffRepo = new();
        public readonly Mock<IRepository<MembershipPlan>> PlanRepo = new();
        public readonly Mock<IRepository<FacilitySchedule>> ScheduleRepo = new();

        private readonly CancellationTokenSource _cts = new();

        public TurnstileTestFixture()
        {
            // ─── Setup default repo behavior ───────────────────────────────────────
            StaffRepo.Setup(r => r.GetByCardIdAsync(It.IsAny<string>(), It.IsAny<Guid?>()))
                     .ReturnsAsync((StaffMember?)null);
            ScheduleRepo.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(new List<FacilitySchedule>());
            PlanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()))
                    .ReturnsAsync((MembershipPlan?)null);

            // ─── Wire real services ────────────────────────────────────────────────
            var facilityCtx = new Mock<IFacilityContextService>();
            facilityCtx.SetupGet(f => f.CurrentFacilityId).Returns(FacilityId);
            facilityCtx.SetupGet(f => f.CurrentFacility).Returns(FacilityType.General);

            var cache = new AccessControlCache();

            var accessControl = new AccessControlService(
                MemberRepo.Object,
                StaffRepo.Object,
                PlanRepo.Object,
                ScheduleRepo.Object,
                facilityCtx.Object,
                cache);

            // Mock ISender for AccessEventService — captures log commands
            var mockSender = new Mock<ISender>();

            // When LogAccessEventCommand is sent, capture the AccessEvent and return success
            mockSender.Setup(s => s.Send(
                It.IsAny<Management.Application.Features.Turnstiles.Commands.LogAccessEvent.LogAccessEventCommand>(),
                It.IsAny<CancellationToken>()))
                .Returns((Management.Application.Features.Turnstiles.Commands.LogAccessEvent.LogAccessEventCommand cmd, CancellationToken _) =>
                {
                    var evt = AccessEvent.Create(cmd.TurnstileId, cmd.CardId, cmd.TransactionId, cmd.Granted,
                        cmd.Granted ? AccessStatus.Granted : AccessStatus.Denied, cmd.Reason ?? "");
                    evt.FacilityId = FacilityId;
                    AccessEventLog.Add(evt);
                    return Task.FromResult<Result<Guid>>(Result.Success(evt.Id));
                });

            var accessEventService = new AccessEventService(mockSender.Object, accessControl);

            // ─── Wire AccessMonitoringWorker with MockTurnstileService ─────────────
            Mock = new MockTurnstileService();
            Mock.ConnectAsync("127.0.0.1", 4370).GetAwaiter().GetResult();

            var scopeFactory = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var scopedProvider = new Mock<IServiceProvider>();

            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
            scope.SetupGet(s => s.ServiceProvider).Returns(scopedProvider.Object);
            var mockMemberService = new Mock<IMemberService>();
            mockMemberService.Setup(m => m.SearchMembersAsync(It.IsAny<Guid>(), It.IsAny<MemberSearchRequest>(), It.IsAny<int>(), It.IsAny<int>()))
                             .ReturnsAsync(Result.Success(new PagedResult<MemberDto> { Items = new List<MemberDto>(), TotalCount = 0 }));

            scopedProvider.Setup(p => p.GetService(typeof(IAccessEventService))).Returns(accessEventService);
            scopedProvider.Setup(p => p.GetService(typeof(IMemberService))).Returns(mockMemberService.Object);

            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            var toast = new Mock<Management.Application.Interfaces.App.IToastService>();

            var config = new Management.Infrastructure.Configuration.TurnstileConfig
            {
                UseMock = true,
                FacilityId = FacilityId
            };

            Worker = new AccessMonitoringWorker(
                Mock,
                scopeFactory.Object,
                NullLogger<AccessMonitoringWorker>.Instance,
                config);
        }

        /// <summary>Seeds a member into the mock repo.</summary>
        public Member SeedActiveMember(string cardId, string name = "Test Member", int sessions = 0, Guid? planId = null)
        {
            var member = new Member(
                id: Guid.NewGuid(),
                fullName: name,
                email: $"{name.Replace(" ", "").ToLower()}@test.com",
                phoneNumber: "+213555000000",
                cardId: cardId,
                profileImageUrl: "",
                status: MemberStatus.Active,
                startDate: DateTime.UtcNow.AddDays(-30),
                expirationDate: DateTime.UtcNow.AddDays(90),
                membershipPlanId: planId,
                gender: Gender.Male,
                remainingSessions: sessions);

            member.FacilityId = FacilityId;
            member.TenantId = TenantId;

            MemberRepo.Setup(r => r.GetByCardIdAsync(cardId, It.IsAny<Guid?>())).ReturnsAsync(member);
            return member;
        }

        /// <summary>Starts the worker and waits for it to subscribe to events.</summary>
        public async Task StartWorkerAsync()
        {
            _ = Worker.StartAsync(_cts.Token);
            await Task.Delay(150);
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }

    // ─── INTEGRATION TESTS ───────────────────────────────────────────────────────

    public class TurnstileIntegrationTests : IAsyncLifetime
    {
        private readonly TurnstileTestFixture _fixture = new();

        public async Task InitializeAsync() => await _fixture.StartWorkerAsync();
        public Task DisposeAsync() { _fixture.Dispose(); return Task.CompletedTask; }

        [Fact]
        public async Task ValidMemberScan_ShouldGrantAccessAndOpenGate()
        {
            // Arrange
            _fixture.SeedActiveMember("CARD-ALICE");

            // Act
            _fixture.Mock.SimulatePhysicalScan("CARD-ALICE");
            await Task.Delay(500);

            // Assert: AccessEvent was logged with granted=true
            _fixture.AccessEventLog.Should().ContainSingle(e =>
                e.CardId == "CARD-ALICE" && e.IsAccessGranted == true,
                "a granted AccessEvent should be logged for Alice");

            // Assert: Gate was opened
            _fixture.Mock.GateOpenedCount.Should().Be(1,
                "the physical gate should open once for a valid scan");
        }

        [Fact]
        public async Task UnknownCardScan_ShouldDenyAndNotOpenGate()
        {
            // Arrange: No member registered with this card — MemberRepo returns null
            _fixture.MemberRepo.Setup(r => r.GetByCardIdAsync("GHOST-999", It.IsAny<Guid?>()))
                               .ReturnsAsync((Member?)null);

            // Act
            _fixture.Mock.SimulatePhysicalScan("GHOST-999");
            await Task.Delay(500);

            // Assert: AccessEvent logged with denied
            _fixture.AccessEventLog.Should().ContainSingle(e =>
                e.CardId == "GHOST-999" && e.IsAccessGranted == false,
                "a denied AccessEvent should be logged for an unknown card");

            // Assert: Gate was NOT opened
            _fixture.Mock.GateOpenedCount.Should().Be(0,
                "the gate must NOT open for an unknown card");
        }

        [Fact]
        public async Task RaceConditionProtection_10RapidScans_ShouldProcessOnlyOnce()
        {
            // Arrange
            _fixture.SeedActiveMember("CARD-BOB", "Bob", sessions: 5);

            // Act: fire 10 rapid scans for the same card
            for (int i = 0; i < 10; i++)
            {
                _fixture.Mock.SimulatePhysicalScan("CARD-BOB");
            }
            await Task.Delay(1000);

            // Assert: Only 1 AccessEvent logged (9 were dropped by dedup)
            _fixture.AccessEventLog.Where(e => e.CardId == "CARD-BOB")
                .Should().HaveCount(1, "deduplication must block 9 of 10 rapid scans");

            // Assert: Gate opened exactly once
            _fixture.Mock.GateOpenedCount.Should().Be(1,
                "gate should open exactly once despite 10 rapid scans");
        }
    }
}
