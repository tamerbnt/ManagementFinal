using Management.Application.DTOs;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard
{
    public interface IDashboardAggregator
    {
        int Priority { get; }
        bool CanHandle(DashboardContext context);
        Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context);
    }
}
