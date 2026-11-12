using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Features.Appointments.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// Appointment endpoints. Thin by design: bind input, call the Application
/// service, shape the HTTP result. All rules (scope, overlap, roles, validation)
/// live in <see cref="IAppointmentService"/> and the domain. Responses carry only
/// administrative appointment data; no medical data is exposed here.
/// </summary>
// TODO (Q-07): Add [Authorize(Roles = "Doctor,Secretary")] once authentication is implemented by its owner.
[ApiController]
[Route("api/appointments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;

    public AppointmentsController(IAppointmentService appointments)
    {
        _appointments = appointments;
    }

    /// <summary>Books a new appointment.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> Book(
        [FromBody] BookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await _appointments.BookAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
    }

    /// <summary>Gets one appointment.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        await _appointments.GetAsync(id, cancellationToken);

    /// <summary>Lists appointments with optional filters and paging.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AppointmentResponse>>> List(
        [FromQuery] ListAppointmentsQuery query,
        CancellationToken cancellationToken) =>
        await _appointments.ListAsync(query, cancellationToken);

    /// <summary>Doctors the caller can book: themself, or a secretary's assigned active doctors.</summary>
    [HttpGet("lookups/doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorLookupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DoctorLookupResponse>>> GetDoctorLookups(
        CancellationToken cancellationToken) =>
        Ok(await _appointments.GetDoctorLookupsAsync(cancellationToken));

    /// <summary>
    /// Searches the patients of one doctor by name or phone. Returns only id, name and phone;
    /// no medical data.
    /// </summary>
    [HttpGet("lookups/patients")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PatientLookupResponse>>> SearchPatientLookups(
        [FromQuery] PatientLookupQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _appointments.SearchPatientsAsync(query, cancellationToken));

    /// <summary>Moves an appointment to a new date and time (same patient and doctor).</summary>
    [HttpPut("{id:guid}/schedule")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> Reschedule(
        Guid id,
        [FromBody] RescheduleAppointmentRequest request,
        CancellationToken cancellationToken) =>
        await _appointments.RescheduleAsync(id, request, cancellationToken);

    /// <summary>Cancels an appointment; a reason is required.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> Cancel(
        Guid id,
        [FromBody] CancelAppointmentRequest request,
        CancellationToken cancellationToken) =>
        await _appointments.CancelAsync(id, request, cancellationToken);

    /// <summary>Confirms, completes or marks an appointment as no-show.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken) =>
        await _appointments.UpdateStatusAsync(id, request, cancellationToken);
}
