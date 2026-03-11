using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Management.Application.Services;
using Management.Domain.Models;
using System.Linq;
using System.Text.Json;

namespace Management.Infrastructure.Services
{
    /// <summary>
    /// High-performance singleton cache for parsed access control schedules.
    /// Prevents repeated JSON parsing during high-volume turnstile events.
    /// </summary>
    public class AccessControlCache : IAccessControlCache
    {
        private readonly ConcurrentDictionary<Guid, List<ScheduleWindow>> _planSchedules = new();
        private List<ScheduleWindow> _facilitySchedules = new();
        private readonly object _facilityLock = new();

        public void UpdatePlanSchedule(Guid planId, string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                _planSchedules.TryRemove(planId, out _);
                return;
            }

            try
            {
                var windows = JsonSerializer.Deserialize<List<ScheduleWindow>>(json);
                if (windows != null)
                {
                    _planSchedules[planId] = windows;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AccessControlCache] Failed to parse schedule for Plan {planId}");
            }
        }

        public void UpdateFacilitySchedules(IEnumerable<ScheduleWindow> schedules)
        {
            lock (_facilityLock)
            {
                _facilitySchedules = schedules.ToList();
            }
        }

        public List<ScheduleWindow> GetPlanSchedule(Guid planId)
        {
            return _planSchedules.TryGetValue(planId, out var windows) ? windows : new List<ScheduleWindow>();
        }

        public List<ScheduleWindow> GetFacilitySchedules()
        {
            lock (_facilityLock)
            {
                return _facilitySchedules;
            }
        }

        public void InvalidatePlanSchedule(Guid planId)
        {
            _planSchedules.TryRemove(planId, out _);
        }

        public void InvalidateFacilitySchedules()
        {
            lock (_facilityLock)
            {
                _facilitySchedules.Clear();
            }
        }

        public void Clear()
        {
            _planSchedules.Clear();
            lock (_facilityLock)
            {
                _facilitySchedules.Clear();
            }
        }
    }
}
