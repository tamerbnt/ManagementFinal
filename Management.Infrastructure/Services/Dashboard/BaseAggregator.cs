using Management.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard
{
    public abstract class BaseAggregator : IDashboardAggregator
    {
        public abstract int Priority { get; }

        public abstract bool CanHandle(DashboardContext context);

        public abstract Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context);

        protected decimal CalculatePercentageChange(decimal previous, decimal current)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return ((current - previous) / previous) * 100;
        }
    }
}
