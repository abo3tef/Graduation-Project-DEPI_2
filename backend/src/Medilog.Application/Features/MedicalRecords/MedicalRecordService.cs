using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.MedicalRecords.Requests;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Application.Features.MedicalRecords.Validators;
using Medilog.Application.Interfaces;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.MedicalRecords;

/// <inheritdoc cref="IMedicalRecordService"/>
public sealed class MedicalRecordService : IMedicalRecordService
{
    private readonly IMedicalRecordStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly IClinicClock _clock;
    private readonly IValidator<GetMedicalProfileRequest> _profileValidator;
    private readonly IValidator<GetMedicalHistoryRequest> _historyValidator;
    private readonly IValidator<ListPatientNotesRequest> _notesValidator;
    private readonly IValidator<ListPatientLabResultsRequest> _labResultsValidator;
    private readonly IValidator<ListPatientTimelineRequest> _timelineValidator;
    private readonly IValidator<ListPatientDocumentsRequest> _documentsValidator;

    public MedicalRecordService(
        IMedicalRecordStore store,
        ICurrentUser currentUser,
        IClinicClock clock,
        IValidator<GetMedicalProfileRequest> profileValidator,
        IValidator<GetMedicalHistoryRequest> historyValidator,
        IValidator<ListPatientNotesRequest> notesValidator,
        IValidator<ListPatientLabResultsRequest> labResultsValidator,
        IValidator<ListPatientTimelineRequest> timelineValidator,
        IValidator<ListPatientDocumentsRequest> documentsValidator)
    {
        _store = store;
        _currentUser = currentUser;
        _clock = clock;
        _profileValidator = profileValidator;
        _historyValidator = historyValidator;
        _notesValidator = notesValidator;
        _labResultsValidator = labResultsValidator;
        _timelineValidator = timelineValidator;
        _documentsValidator = documentsValidator;
    }

    public async Task<PatientMedicalProfileResponse> GetMedicalProfileAsync(
        GetMedicalProfileRequest request,
        CancellationToken cancellationToken)
    {
        // Role and identity first: a non-doctor must not reach validation or any
        // store call (BRULE-10, AGENTS §5).
        EnsureDoctorRole();

        await RequestValidation.ValidateAsync(_profileValidator, request, cancellationToken);

        await RequireActiveDoctorAsync(cancellationToken);
        await EnsurePatientAccessAsync(request.PatientId, cancellationToken);

        var data = await _store.GetProfileDataAsync(request.PatientId, cancellationToken)
            ?? throw new NotFoundException("The patient was not found.", ErrorCodes.PatientNotFound);

        return new PatientMedicalProfileResponse(
            data.PatientId,
            data.FullName,
            data.DateOfBirth,
            CalculateAge(data.DateOfBirth),
            data.Gender,
            data.Phone,
            data.Address,
            data.BloodType,
            data.EmergencyContact,
            OrderAllergies(data.Allergies),
            OrderConditions(data.ChronicConditions));
    }

    public async Task<PagedResult<MedicalVisitResponse>> GetMedicalHistoryAsync(
        GetMedicalHistoryRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDoctorRole();

        await RequestValidation.ValidateAsync(_historyValidator, request, cancellationToken);

        await RequireActiveDoctorAsync(cancellationToken);
        await EnsurePatientAccessAsync(request.PatientId, cancellationToken);

        var page = await _store.ListVisitsAsync(
            request.PatientId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = page.Items.Select(ToVisitResponse).ToArray();

        return new PagedResult<MedicalVisitResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResult<PatientNoteResponse>> ListPatientNotesAsync(
        ListPatientNotesRequest request,
        CancellationToken cancellationToken)
    {
        // Same authorization order as every F3 read use case: the boundary
        // decision happens before validation or persistence.
        EnsureDoctorRole();

        await RequestValidation.ValidateAsync(_notesValidator, request, cancellationToken);

        await RequireActiveDoctorAsync(cancellationToken);
        await EnsurePatientAccessAsync(request.PatientId, cancellationToken);

        var page = await _store.ListNotesAsync(
            request.PatientId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = page.Items
            .Select(note => new PatientNoteResponse(
                note.Id,
                note.VisitId,
                note.DoctorId,
                note.DoctorName,
                note.NoteContent,
                note.CreatedAtUtc,
                note.UpdatedAtUtc))
            .ToArray();

        return new PagedResult<PatientNoteResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResult<PatientLabResultResponse>> ListPatientLabResultsAsync(
        ListPatientLabResultsRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDoctorRole();

        await RequestValidation.ValidateAsync(_labResultsValidator, request, cancellationToken);

        await RequireActiveDoctorAsync(cancellationToken);
        await EnsurePatientAccessAsync(request.PatientId, cancellationToken);

        var page = await _store.ListLabResultsAsync(
            request.PatientId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = page.Items
            .Select(labResult => new PatientLabResultResponse(
                labResult.Id,
                labResult.VisitId,
                labResult.TestName,
                labResult.TestDate,
                labResult.Value,
                labResult.Unit,
                labResult.ReferenceRange,
                labResult.Notes))
            .ToArray();

        return new PagedResult<PatientLabResultResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResult<PatientTimelineEventResponse>> ListPatientTimelineAsync(
        ListPatientTimelineRequest request,
        CancellationToken cancellationToken)
    {
        // Same authorization order as every F3 read use case: the boundary
        // decision happens before validation or persistence.
        EnsureDoctorRole();

        await RequestValidation.ValidateAsync(_timelineValidator, request, cancellationToken);

        await RequireActiveDoctorAsync(cancellationToken);
        await EnsurePatientAccessAsync(request.PatientId, cancellationToken);

        var page = await _store.ListTimelineAsync(
            request.PatientId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = page.Items
            .Select(ToTimelineEventResponse)
            .ToArray();

        return new PagedResult<PatientTimelineEventResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResult<PatientDocumentResponse>> ListPatientDocumentsAsync(
        ListPatientDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDoctorRole();

        await RequestValidation.ValidateAsync(_documentsValidator, request, cancellationToken);

        await RequireActiveDoctorAsync(cancellationToken);
        await EnsurePatientAccessAsync(request.PatientId, cancellationToken);

        var page = await _store.ListDocumentsAsync(
            request.PatientId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = page.Items
            .Select(document => new PatientDocumentResponse(
                document.Id,
                document.FileName,
                document.Type,
                document.UploadDateUtc,
                document.Description,
                document.UploadedByDoctorName))
            .ToArray();

        return new PagedResult<PatientDocumentResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>
    /// The medical-data boundary: only doctors may read records, decided before
    /// validation or persistence (the store must observe zero calls for a
    /// secretary). An unauthenticated or empty identity is an identity failure,
    /// not an access rule.
    /// </summary>
    private void EnsureDoctorRole()
    {
        if (_currentUser.Role is not UserRole.Doctor)
        {
            throw new ForbiddenException(
                "Medical records are only available to doctors.",
                ErrorCodes.MedicalRecordAccessDenied);
        }

        if (_currentUser.UserId is not { } userId || userId == Guid.Empty)
        {
            throw new ForbiddenException(
                "The current user cannot access medical records.",
                ErrorCodes.IdentityInvalid);
        }
    }

    private async Task RequireActiveDoctorAsync(CancellationToken cancellationToken)
    {
        var doctor = await _store.GetDoctorAsync(_currentUser.UserId!.Value, cancellationToken);

        if (doctor is null || !doctor.IsActive)
        {
            throw new ForbiddenException(
                "The current user cannot access medical records.",
                ErrorCodes.IdentityInvalid);
        }
    }

    /// <summary>
    /// The workspace scope (BRULE-03/04): the patient must exist and be owned by
    /// the acting doctor. The decision itself is the team's single
    /// <see cref="MedicalRecordAccessPolicy"/>; all failure cases report the same
    /// not-found result so out-of-scope patients are not revealed.
    /// </summary>
    private async Task EnsurePatientAccessAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var owner = await _store.GetPatientOwnerAsync(patientId, cancellationToken);

        if (owner is null || !MedicalRecordAccessPolicy.CanAccessPatient(owner.DoctorId, _currentUser.UserId!.Value))
        {
            throw new NotFoundException("The patient was not found.", ErrorCodes.PatientNotFound);
        }
    }

    /// <summary>Age in whole years at the clinic-local current date (birthday-safe, incl. Feb 29).</summary>
    private int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(_clock.LocalNow.Date);
        var age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    /// <summary>
    /// Maps one flat timeline projection to the discriminated response
    /// (F3-C3b Q-12/D-T1…D-T3): exactly one payload is set, matching the event
    /// type. An unknown event type is a contract violation and fails loudly.
    /// </summary>
    private static PatientTimelineEventResponse ToTimelineEventResponse(PatientTimelineEventData data) =>
        data.EventType switch
        {
            TimelineEventTypes.Visit => new(
                data.Id,
                data.EventType,
                data.OccurredAtUtc,
                data.VisitId,
                new PatientTimelineVisitPayloadResponse(
                    data.DoctorName ?? string.Empty,
                    data.ChiefComplaint,
                    data.DiagnosesCount,
                    data.MedicationsCount,
                    data.NotesCount,
                    data.LabResultsCount),
                null,
                null,
                null),
            TimelineEventTypes.Note => new(
                data.Id,
                data.EventType,
                data.OccurredAtUtc,
                data.VisitId,
                null,
                new PatientTimelineNotePayloadResponse(
                    data.DoctorName ?? string.Empty,
                    data.NoteContent ?? string.Empty),
                null,
                null),
            TimelineEventTypes.LabResult => new(
                data.Id,
                data.EventType,
                data.OccurredAtUtc,
                data.VisitId,
                null,
                null,
                new PatientTimelineLabPayloadResponse(
                    data.TestName ?? string.Empty,
                    data.TestValue ?? 0m,
                    data.TestUnit ?? string.Empty),
                null),
            TimelineEventTypes.Document => new(
                data.Id,
                data.EventType,
                data.OccurredAtUtc,
                data.VisitId,
                null,
                null,
                null,
                new PatientTimelineDocumentPayloadResponse(
                    data.FileName ?? string.Empty,
                    data.FileType ?? MedicalDocumentType.Pdf,
                    data.DocumentDescription,
                    data.DoctorName)),
            _ => throw new InvalidOperationException(
                $"Unknown timeline event type '{data.EventType}'.")
        };

    // Deterministic response ordering (task file D-13). The enum values are
    // stored as their string labels in the database, so the severity/status
    // ranking can only be expressed here, not in SQL.

    private static IReadOnlyList<PatientAllergyResponse> OrderAllergies(IReadOnlyList<PatientAllergyData> source) =>
        source
            .OrderBy(allergy => SeverityRank(allergy.Severity))
            .ThenBy(allergy => allergy.Name, StringComparer.Ordinal)
            .ThenBy(allergy => allergy.Id)
            .Select(allergy => new PatientAllergyResponse(
                allergy.Id,
                allergy.Name,
                allergy.Severity,
                allergy.Description,
                allergy.Notes))
            .ToArray();

    private static IReadOnlyList<PatientChronicConditionResponse> OrderConditions(
        IReadOnlyList<PatientConditionData> source) =>
        source
            .OrderBy(condition => StatusRank(condition.Status))
            // Nulls last: the sentinel is the smallest value, so descending puts it at the end.
            .ThenByDescending(condition => condition.DiagnosisDate ?? DateOnly.MinValue)
            .ThenBy(condition => condition.Name, StringComparer.Ordinal)
            .ThenBy(condition => condition.Id)
            .Select(condition => new PatientChronicConditionResponse(
                condition.Id,
                condition.Name,
                condition.DiagnosisDate,
                condition.Status,
                condition.Notes))
            .ToArray();

    private static int SeverityRank(AllergySeverity? severity) => severity switch
    {
        AllergySeverity.Severe => 0,
        AllergySeverity.Moderate => 1,
        AllergySeverity.Mild => 2,
        _ => 3
    };

    private static int StatusRank(ChronicConditionStatus? status) => status switch
    {
        ChronicConditionStatus.Active => 0,
        ChronicConditionStatus.Controlled => 1,
        ChronicConditionStatus.Resolved => 2,
        _ => 3
    };

    private static MedicalVisitResponse ToVisitResponse(MedicalVisitData visit) => new(
        visit.VisitId,
        visit.VisitDate,
        visit.DoctorId,
        visit.DoctorName,
        visit.ChiefComplaint,
        visit.Symptoms,
        visit.ClinicalNotes,
        visit.Diagnoses
            .OrderByDescending(diagnosis => diagnosis.DiagnosisDate)
            .ThenBy(diagnosis => diagnosis.Name, StringComparer.Ordinal)
            .ThenBy(diagnosis => diagnosis.Id)
            .Select(diagnosis => new VisitDiagnosisResponse(
                diagnosis.Id,
                diagnosis.Name,
                diagnosis.Description,
                diagnosis.DiagnosisDate))
            .ToArray(),
        visit.Medications
            .OrderBy(medication => medication.Name, StringComparer.Ordinal)
            .ThenBy(medication => medication.Id)
            .Select(medication => new VisitMedicationResponse(
                medication.Id,
                medication.Name,
                medication.Dosage,
                medication.Frequency,
                medication.Duration,
                medication.Instructions))
            .ToArray(),
        visit.VitalSigns
            .OrderBy(vitalSigns => vitalSigns.Id)
            .Select(vitalSigns => new VisitVitalSignsResponse(
                vitalSigns.Id,
                vitalSigns.BloodPressure,
                vitalSigns.HeartRate,
                vitalSigns.Temperature,
                vitalSigns.RespiratoryRate,
                vitalSigns.Weight,
                vitalSigns.Height,
                vitalSigns.SpO2))
            .ToArray(),
        visit.Notes
            .OrderByDescending(note => note.CreatedAtUtc)
            .ThenBy(note => note.Id)
            .Select(note => new VisitNoteResponse(
                note.Id,
                note.DoctorId,
                note.DoctorName,
                note.Content,
                note.CreatedAtUtc,
                note.UpdatedAtUtc))
            .ToArray(),
        visit.LabResults
            .OrderByDescending(labResult => labResult.TestDate)
            .ThenBy(labResult => labResult.TestName, StringComparer.Ordinal)
            .ThenBy(labResult => labResult.Id)
            .Select(labResult => new VisitLabResultResponse(
                labResult.Id,
                labResult.TestName,
                labResult.TestDate,
                labResult.Value,
                labResult.Unit,
                labResult.ReferenceRange,
                labResult.Notes))
            .ToArray());
}
