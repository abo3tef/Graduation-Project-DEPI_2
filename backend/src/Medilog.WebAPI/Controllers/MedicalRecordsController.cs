using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Requests;
using Medilog.Application.Features.MedicalRecords.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// HTTP surface for reading a patient's medical profile and paged medical history (F3-C2).
/// </summary>
/// <remarks>
/// Every action is a thin adapter over <see cref="IMedicalRecordService"/>: no validation, query
/// or ownership rule lives here (AGENTS.md §4.1, §4.4). The acting doctor is resolved from the
/// authenticated principal inside the service (D-11); a non-doctor principal is rejected there
/// with <c>403 medical.access_denied</c> before any data access, and a patient outside the
/// acting doctor's workspace reports the same <c>404 patient.not_found</c> as an unknown one.
/// Routes are patient-centric (F3-C2 P-1) and do not touch the visit-centric F4 routes under
/// <c>api/medical-visits</c>.
/// </remarks>
[ApiController]
[Route("api/patients/{patientId:guid}")]
[Authorize(Roles = Roles.Doctor)]
public sealed class MedicalRecordsController : ControllerBase
{
    private readonly IMedicalRecordService _medicalRecords;

    public MedicalRecordsController(IMedicalRecordService medicalRecords)
    {
        _medicalRecords = medicalRecords;
    }

    /// <summary>Returns the medical profile of one of the acting doctor's own patients.</summary>
    [HttpGet("medical-profile")]
    [ProducesResponseType(typeof(PatientMedicalProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientMedicalProfileResponse>> GetMedicalProfileAsync(
        Guid patientId,
        CancellationToken cancellationToken)
    {
        var response = await _medicalRecords.GetMedicalProfileAsync(
            new GetMedicalProfileRequest(patientId),
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Returns one page of the patient's previous visits with their linked clinical records,
    /// newest first. Paging follows the shared Q-04 contract: <c>pageNumber</c> (default 1) and
    /// <c>pageSize</c> (default 10, maximum 50), validated by the service.
    /// </summary>
    /// <remarks>
    /// The paging values bind as scalars (instead of model-binding the whole request record)
    /// because the patient id arrives in the route, not the query string; the shared defaults
    /// still come only from <see cref="PagingDefaults"/>.
    /// </remarks>
    [HttpGet("medical-visits")]
    [ProducesResponseType(typeof(PagedResult<MedicalVisitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<MedicalVisitResponse>>> ListMedicalVisitsAsync(
        Guid patientId,
        [FromQuery(Name = "pageNumber")] int pageNumber = PagingDefaults.DefaultPageNumber,
        [FromQuery(Name = "pageSize")] int pageSize = PagingDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var response = await _medicalRecords.GetMedicalHistoryAsync(
            new GetMedicalHistoryRequest(patientId, pageNumber, pageSize),
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Returns every clinical note of the patient — visit-linked and unlinked
    /// (F3-C3a) — newest first. Paging follows the shared Q-04 contract.
    /// </summary>
    [HttpGet("notes")]
    [ProducesResponseType(typeof(PagedResult<PatientNoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<PatientNoteResponse>>> ListNotesAsync(
        Guid patientId,
        [FromQuery(Name = "pageNumber")] int pageNumber = PagingDefaults.DefaultPageNumber,
        [FromQuery(Name = "pageSize")] int pageSize = PagingDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var response = await _medicalRecords.ListPatientNotesAsync(
            new ListPatientNotesRequest(patientId, pageNumber, pageSize),
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Returns every laboratory result of the patient — visit-linked and
    /// unlinked (F3-C3a) — newest test first. Paging follows the shared Q-04 contract.
    /// </summary>
    [HttpGet("lab-results")]
    [ProducesResponseType(typeof(PagedResult<PatientLabResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<PatientLabResultResponse>>> ListLabResultsAsync(
        Guid patientId,
        [FromQuery(Name = "pageNumber")] int pageNumber = PagingDefaults.DefaultPageNumber,
        [FromQuery(Name = "pageSize")] int pageSize = PagingDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var response = await _medicalRecords.ListPatientLabResultsAsync(
            new ListPatientLabResultsRequest(patientId, pageNumber, pageSize),
            cancellationToken);

        return Ok(response);
    }
}
