using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Patients;
using Medilog.Application.Features.Patients.Requests;
using Medilog.Application.Features.Patients.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// Patient management endpoints (F8). Thin by design: bind input, call <see cref="IPatientService"/>,
/// shape the HTTP result. Scope (own patients / assigned doctors), the secretary's administrative-only
/// view (Q-18) and the delete guard (Q-17) live in the Application service.
/// </summary>
/// <remarks>
/// Routes sit beside, and do not overlap, the patient-centric medical routes of
/// <see cref="MedicalRecordsController"/> (<c>api/patients/{patientId:guid}/medical-profile</c> etc.):
/// every route here is either the collection, the literal <c>lookup</c> or a bare <c>{id:guid}</c>.
/// </remarks>
[ApiController]
[Route("api/patients")]
[Produces("application/json")]
[Authorize(Roles = Roles.Doctor + "," + Roles.Secretary)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class PatientsController : ControllerBase
{
    private readonly IPatientService _patients;

    public PatientsController(IPatientService patients)
    {
        _patients = patients;
    }

    /// <summary>Creates a patient in the caller's workspace (a secretary names an assigned doctor).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientResponse>> Create(
        [FromBody] CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        var patient = await _patients.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
    }

    /// <summary>Lists the patients in the caller's scope with search, filters and paging.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PatientResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PatientResponse>>> List(
        [FromQuery] ListPatientsQuery query,
        CancellationToken cancellationToken) =>
        await _patients.ListAsync(query, cancellationToken);

    /// <summary>Small bounded search (id, name, phone) for the patient picker.</summary>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientLookupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PatientLookupResponse>>> Lookup(
        [FromQuery] PatientPickerQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _patients.LookupAsync(query, cancellationToken));

    /// <summary>Gets one patient in the caller's scope.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        await _patients.GetAsync(id, cancellationToken);

    /// <summary>Updates the editable fields of a patient in the caller's scope.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientResponse>> Update(
        Guid id,
        [FromBody] UpdatePatientRequest request,
        CancellationToken cancellationToken) =>
        await _patients.UpdateAsync(id, request, cancellationToken);

    /// <summary>Deletes a patient that has no related records (doctor only); otherwise 409.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _patients.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
