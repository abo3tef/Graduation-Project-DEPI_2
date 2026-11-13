using Medilog.Application.Common.Security;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// HTTP surface for recording and updating medical visits, their diagnoses and prescribed
/// medication lines.
/// </summary>
/// <remarks>
/// Every action is a thin adapter over <see cref="IMedicalVisitService"/>: no validation, query or
/// ownership rule lives here (AGENTS.md §4.1, §4.4). The acting doctor is resolved from the
/// authenticated principal inside the service, never from the request (D-11).
/// </remarks>
[ApiController]
[Route("api/medical-visits")]
[Authorize(Roles = Roles.Doctor)]
public sealed class MedicalVisitsController : ControllerBase
{
    /// <summary>
    /// Named route used to build the <c>Location</c> header of created resources (D-20). A named
    /// route is used instead of <c>CreatedAtAction</c> because MVC suppresses the <c>Async</c>
    /// suffix in action names by default, which would break action-name based link generation.
    /// </summary>
    private const string GetVisitRouteName = "MedicalVisits.GetVisit";

    private readonly IMedicalVisitService _medicalVisitService;

    public MedicalVisitsController(IMedicalVisitService medicalVisitService)
    {
        _medicalVisitService = medicalVisitService;
    }

    /// <summary>Records a new medical visit for a patient in the doctor's workspace.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MedicalVisitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MedicalVisitResponse>> CreateVisitAsync(
        [FromBody] CreateMedicalVisitRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _medicalVisitService.CreateVisitAsync(request, cancellationToken);

        return CreatedAtRoute(GetVisitRouteName, new { visitId = response.Id }, response);
    }

    /// <summary>Returns one visit, scoped to the acting doctor.</summary>
    [HttpGet("{visitId:guid}", Name = GetVisitRouteName)]
    [ProducesResponseType(typeof(MedicalVisitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicalVisitResponse>> GetVisitAsync(
        Guid visitId,
        CancellationToken cancellationToken)
    {
        var response = await _medicalVisitService.GetVisitAsync(visitId, cancellationToken);

        return Ok(response);
    }

    /// <summary>Updates the mutable fields of an existing visit.</summary>
    [HttpPut("{visitId:guid}")]
    [ProducesResponseType(typeof(MedicalVisitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicalVisitResponse>> UpdateVisitAsync(
        Guid visitId,
        [FromBody] UpdateMedicalVisitRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _medicalVisitService.UpdateVisitAsync(visitId, request, cancellationToken);

        return Ok(response);
    }

    /// <summary>Adds a diagnosis to an existing visit.</summary>
    [HttpPost("{visitId:guid}/diagnoses")]
    [ProducesResponseType(typeof(DiagnosisResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiagnosisResponse>> AddDiagnosisAsync(
        Guid visitId,
        [FromBody] CreateDiagnosisRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _medicalVisitService.AddDiagnosisAsync(visitId, request, cancellationToken);

        // There is no GET diagnosis route in the approved contract, so Location points at the
        // parent visit that contains the diagnosis (D-20).
        return CreatedAtRoute(GetVisitRouteName, new { visitId }, response);
    }

    /// <summary>Updates the mutable fields of a diagnosis that belongs to the visit.</summary>
    [HttpPut("{visitId:guid}/diagnoses/{diagnosisId:guid}")]
    [ProducesResponseType(typeof(DiagnosisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiagnosisResponse>> UpdateDiagnosisAsync(
        Guid visitId,
        Guid diagnosisId,
        [FromBody] UpdateDiagnosisRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _medicalVisitService.UpdateDiagnosisAsync(
            visitId,
            diagnosisId,
            request,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>Adds a prescribed medication line to an existing visit.</summary>
    [HttpPost("{visitId:guid}/medications")]
    [ProducesResponseType(typeof(MedicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicationResponse>> AddMedicationAsync(
        Guid visitId,
        [FromBody] CreateMedicationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _medicalVisitService.AddMedicationAsync(visitId, request, cancellationToken);

        // There is no GET medication route, so Location points at the parent visit that contains
        // the medication (D-20, D-24).
        return CreatedAtRoute(GetVisitRouteName, new { visitId }, response);
    }

    /// <summary>Updates the mutable fields of a medication that belongs to the visit.</summary>
    [HttpPut("{visitId:guid}/medications/{medicationId:guid}")]
    [ProducesResponseType(typeof(MedicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicationResponse>> UpdateMedicationAsync(
        Guid visitId,
        Guid medicationId,
        [FromBody] UpdateMedicationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _medicalVisitService.UpdateMedicationAsync(
            visitId,
            medicationId,
            request,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>Deletes a medication line that was entered by mistake (D-23).</summary>
    [HttpDelete("{visitId:guid}/medications/{medicationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMedicationAsync(
        Guid visitId,
        Guid medicationId,
        CancellationToken cancellationToken)
    {
        await _medicalVisitService.DeleteMedicationAsync(visitId, medicationId, cancellationToken);

        return NoContent();
    }
}
