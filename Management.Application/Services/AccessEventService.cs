using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;

namespace Management.Application.Services
{
    public class AccessEventService : IAccessEventService
    {
        private readonly IAccessEventRepository _accessRepository;
        private readonly ITurnstileRepository _turnstileRepository;
        private readonly AccessEventStore _accessStore;
        private readonly Random _random = new Random();

        public AccessEventService(
            IAccessEventRepository accessRepository,
            ITurnstileRepository turnstileRepository,
            AccessEventStore accessStore)
        {
            _accessRepository = accessRepository;
            _turnstileRepository = turnstileRepository;
            _accessStore = accessStore;
        }

        public async Task<List<AccessEventDto>> GetRecentEventsAsync(int count = 50)
        {
            var entities = await _accessRepository.GetRecentEventsAsync(count);
            return entities.Select(MapToDto).ToList();
        }

        public async Task<List<AccessEventDto>> GetEventsByRangeAsync(DateTime start, DateTime end)
        {
            var entities = await _accessRepository.GetByDateRangeAsync(start, end);
            return entities.Select(MapToDto).ToList();
        }

        public async Task<int> GetCurrentOccupancyAsync()
        {
            return await _accessRepository.GetCurrentOccupancyCountAsync();
        }

        public async Task SimulateScanAsync(Guid? turnstileId = null)
        {
            // 1. Generate Fake Data
            bool isGranted = _random.NextDouble() > 0.15; // 85% success rate

            var evt = new AccessEvent
            {
                Timestamp = DateTime.UtcNow,
                TurnstileId = turnstileId,
                FacilityType = FacilityType.Gym,
                IsAccessGranted = isGranted,
                AccessStatus = isGranted ? AccessStatus.Granted : AccessStatus.Denied,
                FailureReason = isGranted ? null : "Membership Expired",
                // Mock Member Data
                MemberId = Guid.NewGuid(),
                MemberNameSnapshot = isGranted ? "Simulated Member" : "Unknown User",
                CardId = "SIM-" + _random.Next(1000, 9999)
            };

            // 2. Persist
            await _accessRepository.AddAsync(evt);

            // 3. Broadcast to UI (Real-Time Feed)
            _accessStore.TriggerAccessEvent(MapToDto(evt));
        }

        private AccessEventDto MapToDto(AccessEvent entity)
        {
            return new AccessEventDto
            {
                Id = entity.Id,
                Timestamp = entity.Timestamp.ToLocalTime(),
                MemberId = entity.MemberId,
                MemberName = entity.MemberNameSnapshot,
                CardId = entity.CardId,
                IsAccessGranted = entity.IsAccessGranted,
                AccessStatus = entity.AccessStatus.ToString(),
                FacilityName = entity.FacilityType.ToString(),
                FailureReason = entity.FailureReason,
                FacilityType = entity.FacilityType
            };
        }
    }
}