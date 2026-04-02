using System;
using System.Collections.Generic;
using Management.Domain.Models;

namespace Management.Application.Services
{
    public interface IAccessControlCache
    {
        void UpdatePlanSchedule(Guid planId, string? json);
        void UpdateFacilitySchedules(IEnumerable<ScheduleWindow> schedules);
        List<ScheduleWindow> GetPlanSchedule(Guid planId);
        List<ScheduleWindow> GetFacilitySchedules();
        void InvalidatePlanSchedule(Guid planId);
        void InvalidateFacilitySchedules();
        bool TryMarkMemberInside(string cardId);
        void MarkMemberExited(string cardId);
        void Clear();
    }
}
