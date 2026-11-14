using Medilog.Application.Common.Security;
using Medilog.Application.Features.Dashboard;
using Medilog.Application.Features.Dashboard.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// HTTP endpoints for the doctor clinic dashboard.
/// </summary>
[ApiController]
[Route("api/dashboard/doctor")]
[Authorize(Roles = Roles.Doctor)]
public sealed class DoctorDashboardController : ControllerBase
{
    private readonly IDoctorDashboardService _dashboardService;

    public DoctorDashboardController(IDoctorDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Returns aggregated dashboard metrics and today's schedule for the acting doctor.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DoctorDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoctorDashboardResponse>> GetDoctorDashboardAsync(CancellationToken cancellationToken)
    {
        var response = await _dashboardService.GetDoctorDashboardAsync(cancellationToken);
        return Ok(response);
    }
}

