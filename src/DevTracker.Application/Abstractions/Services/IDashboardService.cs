using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Dashboard;

namespace DevTracker.Application.Abstractions.Services;

public interface IDashboardService
{
    Task<Result<DashboardMetricsDto>> GetUserMetricsAsync(CancellationToken ct = default);
}
