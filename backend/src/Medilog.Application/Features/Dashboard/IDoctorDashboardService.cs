using Medilog.Application.Features.Dashboard.Contracts;

namespace Medilog.Application.Features.Dashboard;

/// <summary>
/// Service providing aggregated clinic metrics and today's schedule for the authenticated doctor.
/// </summary>
public interface IDoctorDashboardService
{
    /// <summary>
    /// Computes and returns the dashboard metrics and today's appointments for the acting doctor.
    /// </summary>
    Task<DoctorDashboardResponse> GetDoctorDashboardAsync(CancellationToken cancellationToken = default);
}

