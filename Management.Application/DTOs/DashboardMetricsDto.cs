using System.Collections.Generic;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record DashboardMetricsDto
    {
        public int TotalActiveMembers { get; init; }
        public int PendingRegistrationsCount { get; init; }
        public int ActivePeopleCount { get; init; }
        public List<RegistrationDto> RecentRegistrations { get; init; } = new();
        public List<AccessEventDto> ActivityFeed { get; init; } = new();
        
        // Facility-specific
        public int ActiveOrdersCount { get; init; }
        public decimal TodayRevenue { get; init; }
        public decimal TodayExpenses { get; init; }
        public double OccupancyPercentage { get; init; }
    }
}
