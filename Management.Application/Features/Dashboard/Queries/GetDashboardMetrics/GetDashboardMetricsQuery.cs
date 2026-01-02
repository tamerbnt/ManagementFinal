using Management.Application.DTOs;
using MediatR;

namespace Management.Application.Features.Dashboard.Queries.GetDashboardMetrics
{
    public record GetDashboardMetricsQuery() : IRequest<DashboardMetricsDto>;
}
