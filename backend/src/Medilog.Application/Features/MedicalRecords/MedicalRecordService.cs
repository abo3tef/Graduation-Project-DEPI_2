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
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.MedicalRecords;

/// <inheritdoc cref="IMedicalRecordService"/>
public sealed class MedicalRecordService : IMedicalRecordService
{
    private readonly IMedicalRecordStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly IClinicClock _clock;
    private readonly IValidator<GetMedicalProfileRequest> _profileValidator;
    private readonly IValidator<GetMedicalHistoryRequest> _historyValidator;
    private readonly IApplicationDbContext _db;
    private readonly IValidator<GetPatientTimelineRequest> _timelineValidator;

    public MedicalRecordService(
        IMedicalRecordStore store,
        ICurrentUser currentUser,
        IClinicClock clock,
        IValidator<GetMedicalProfileRequest> profileValidator,
        IValidator<GetMedicalHistoryRequest> historyValidator,
        IApplicationDbContext db,
        IValidator<GetPatientTimelineRequest> timelineValidator)
    {
        _store = store;
        _currentUser = currentUser;
        _clock = clock;
        _profileValidator = profileValidator;
        _historyValidator = historyValidator;
        _db = db;
        _timelineValidator = timelineValidator;
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

    /// <summary>
    /// One page of the patient's medical timeline (F10-C1, Q-20): visits,
    /// diagnoses, medications and clinical notes in one chronological feed,
    /// newest first, optionally filtered by event type and date window.
    /// </summary>
    /// <remarks>
    /// Composition is exact but bounded: each source is counted and ordered at
    /// the data source (SQL), and only each source's top
    /// <c>min(skip + pageSize, totalCount)</c> rows are materialized — at most
    /// four bounded page prefixes, never the whole timeline (AGENTS §3.1). The
    /// four counts run first, so a page beyond the last one returns an empty
    /// page without fetching anything. A portable single SQL union is not
    /// available because <see cref="Diagnosis.DiagnosisDate"/> is a calendar
    /// date while the other sources are instants, so the global merge over
    /// those rows happens here with the deterministic contract ordering:
    /// <c>OccurredAt</c> descending, then the event type ascending in true
    /// ordinal order (<c>Diagnosis</c> &lt; <c>MedicalNote</c> &lt;
    /// <c>Medication</c> &lt; <c>Visit</c>), then the id ascending in canonical
    /// GUID string order. The id tie-breaker is the string order on purpose:
    /// the sources order ties by the <c>char(36)</c> id column, whose
    /// collation ordering equals the canonical string order, so
    /// <c>Id.ToString()</c> in the merge matches the SQL exactly and each
    /// source's fetched prefix is its true prefix in the global order (the
    /// InMemory test provider, D-15, evaluates Guid order instead — that only
    /// matters when more rows than the fetch limit fully tie within one
    /// source). With the type constant within a source, the merged page is
    /// always the true global page.
    /// </remarks>
    public async Task<PagedResult<PatientTimelineEventResponse>> GetPatientTimelineAsync(
        GetPatientTimelineRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDoctorRole();

        await RequestValidation.ValidateAsync(_timelineValidator, request, cancellationToken);

        await RequireActiveDoctorAsync(cancellationToken);
        await EnsurePatientAccessAsync(request.PatientId, cancellationToken);

        // The optional type filter is canonicalized once; the validator has
        // already accepted only the four contract spellings (case-insensitively).
        var eventType = NormalizeEventType(request.EventType);

        // Long arithmetic: pageNumber is only bounded from below (>= 1), so the
        // int product could wrap for a huge page number. A page that far out is
        // simply beyond the last page, so it lands in the empty-page branch
        // below with correct metadata — never in undefined arithmetic.
        var skip = (long)(request.PageNumber - 1) * request.PageSize;

        // Pass 1 — counts only (the helpers fetch nothing for take <= 0). The
        // per-source fetch limit is derived from the total count, so a page
        // beyond the last one returns here before anything is fetched.
        var (visitCount, _) = await QueryVisitEventsAsync(
            request.PatientId, eventType, request.From, request.To, take: 0, cancellationToken);
        var (diagnosisCount, _) = await QueryDiagnosisEventsAsync(
            request.PatientId, eventType, request.From, request.To, take: 0, cancellationToken);
        var (medicationCount, _) = await QueryMedicationEventsAsync(
            request.PatientId, eventType, request.From, request.To, take: 0, cancellationToken);
        var (noteCount, _) = await QueryNoteEventsAsync(
            request.PatientId, eventType, request.From, request.To, take: 0, cancellationToken);

        var totalCount = visitCount + diagnosisCount + medicationCount + noteCount;

        // A page beyond the last one stays a success with correct metadata.
        if (skip >= totalCount)
        {
            return new PagedResult<PatientTimelineEventResponse>(
                [], request.PageNumber, request.PageSize, totalCount);
        }

        // Pass 2 — bounded fetch for an existing page. Here skip < totalCount
        // <= int.MaxValue, so both values fit in an int; clamping the fetch
        // window to totalCount never drops a page element (its global rank is
        // < totalCount) and keeps the per-source Take free of any overflow.
        var skipInt = (int)skip;
        var take = (int)Math.Min(skip + request.PageSize, (long)totalCount);

        var (_, visitRows) = await QueryVisitEventsAsync(
            request.PatientId, eventType, request.From, request.To, take, cancellationToken);
        var (_, diagnosisRows) = await QueryDiagnosisEventsAsync(
            request.PatientId, eventType, request.From, request.To, take, cancellationToken);
        var (_, medicationRows) = await QueryMedicationEventsAsync(
            request.PatientId, eventType, request.From, request.To, take, cancellationToken);
        var (_, noteRows) = await QueryNoteEventsAsync(
            request.PatientId, eventType, request.From, request.To, take, cancellationToken);

        var items = visitRows
            .Concat(diagnosisRows)
            .Concat(medicationRows)
            .Concat(noteRows)
            .OrderByDescending(@event => @event.OccurredAt)
            .ThenBy(@event => @event.Type, StringComparer.Ordinal)
            .ThenBy(@event => @event.Id.ToString(), StringComparer.Ordinal)
            .Skip(skipInt)
            .Take(request.PageSize)
            .Select(row => new PatientTimelineEventResponse(
                row.Id,
                row.Type,
                row.OccurredAt,
                row.Title,
                row.Summary,
                row.RelatedVisitId))
            .ToArray();

        return new PagedResult<PatientTimelineEventResponse>(
            items, request.PageNumber, request.PageSize, totalCount);
    }

    /// <summary>
    /// One merged timeline row before the final response mapping. Each source
    /// query is a small column projection; the title/summary composition stays
    /// in CLR so every SQL statement remains provider-agnostic.
    /// </summary>
    private sealed record TimelineRow(
        Guid Id,
        string Type,
        DateTimeOffset OccurredAt,
        string Title,
        string? Summary,
        Guid? RelatedVisitId);

    private async Task<(int TotalCount, List<TimelineRow> Rows)> QueryVisitEventsAsync(
        Guid patientId,
        string? eventType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken)
    {
        if (eventType is { } filter && filter != PatientTimelineEventTypes.Visit)
        {
            return (0, []);
        }

        var query = _db.MedicalVisits
            .AsNoTracking()
            .Where(visit => visit.PatientId == patientId);

        if (from is { } fromDate)
        {
            query = query.Where(visit => visit.VisitDate >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(visit => visit.VisitDate <= toDate);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // take <= 0 means count-only: the caller counts all four sources
        // before deciding whether a page exists at all, so a page beyond the
        // last one never fetches rows.
        if (take <= 0)
        {
            return (totalCount, []);
        }

        var rows = await query
            .OrderByDescending(visit => visit.VisitDate)
            .ThenBy(visit => visit.Id)
            .Take(take)
            .Select(visit => new { visit.Id, visit.VisitDate, visit.ChiefComplaint })
            .ToListAsync(cancellationToken);

        return (totalCount, rows
            .Select(visit => new TimelineRow(
                visit.Id,
                PatientTimelineEventTypes.Visit,
                visit.VisitDate,
                "Medical Visit",
                visit.ChiefComplaint,
                visit.Id))
            .ToList());
    }

    private async Task<(int TotalCount, List<TimelineRow> Rows)> QueryDiagnosisEventsAsync(
        Guid patientId,
        string? eventType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken)
    {
        if (eventType is { } filter && filter != PatientTimelineEventTypes.Diagnosis)
        {
            return (0, []);
        }

        var query = _db.Diagnoses
            .AsNoTracking()
            .Where(diagnosis => diagnosis.PatientId == patientId);

        // The diagnosis date is a calendar date without a time-of-day, so the
        // instant window is applied to it in UTC calendar days (documented in
        // the API contract): a diagnosis on day D belongs to the window when
        // D lies within [date(From), date(To)].
        if (from is { } fromDate)
        {
            var fromDateOnly = DateOnly.FromDateTime(fromDate.UtcDateTime);
            query = query.Where(diagnosis => diagnosis.DiagnosisDate >= fromDateOnly);
        }

        if (to is { } toDate)
        {
            var toDateOnly = DateOnly.FromDateTime(toDate.UtcDateTime);
            query = query.Where(diagnosis => diagnosis.DiagnosisDate <= toDateOnly);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // take <= 0 means count-only (see QueryVisitEventsAsync).
        if (take <= 0)
        {
            return (totalCount, []);
        }

        var rows = await query
            .OrderByDescending(diagnosis => diagnosis.DiagnosisDate)
            .ThenBy(diagnosis => diagnosis.Id)
            .Take(take)
            .Select(diagnosis => new
            {
                diagnosis.Id,
                diagnosis.DiagnosisDate,
                diagnosis.DiagnosisName,
                diagnosis.Description,
                diagnosis.VisitId
            })
            .ToListAsync(cancellationToken);

        return (totalCount, rows
            .Select(diagnosis => new TimelineRow(
                diagnosis.Id,
                PatientTimelineEventTypes.Diagnosis,
                ToUtcInstant(diagnosis.DiagnosisDate),
                diagnosis.DiagnosisName,
                diagnosis.Description,
                diagnosis.VisitId))
            .ToList());
    }

    private async Task<(int TotalCount, List<TimelineRow> Rows)> QueryMedicationEventsAsync(
        Guid patientId,
        string? eventType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken)
    {
        if (eventType is { } filter && filter != PatientTimelineEventTypes.Medication)
        {
            return (0, []);
        }

        var query = _db.Medications
            .AsNoTracking()
            .Where(medication => medication.PatientId == patientId);

        // A prescription item has no date of its own; its timeline instant is
        // the consultation date of the visit that produced it.
        if (from is { } fromDate)
        {
            query = query.Where(medication => medication.Visit.VisitDate >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(medication => medication.Visit.VisitDate <= toDate);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // take <= 0 means count-only (see QueryVisitEventsAsync).
        if (take <= 0)
        {
            return (totalCount, []);
        }

        var rows = await query
            .OrderByDescending(medication => medication.Visit.VisitDate)
            .ThenBy(medication => medication.Id)
            .Take(take)
            .Select(medication => new
            {
                medication.Id,
                medication.MedicationName,
                medication.Dosage,
                medication.Frequency,
                medication.VisitId,
                VisitDate = medication.Visit.VisitDate
            })
            .ToListAsync(cancellationToken);

        return (totalCount, rows
            .Select(medication => new TimelineRow(
                medication.Id,
                PatientTimelineEventTypes.Medication,
                medication.VisitDate,
                medication.MedicationName,
                DescribeMedication(medication.Dosage, medication.Frequency),
                medication.VisitId))
            .ToList());
    }

    private async Task<(int TotalCount, List<TimelineRow> Rows)> QueryNoteEventsAsync(
        Guid patientId,
        string? eventType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken)
    {
        if (eventType is { } filter && filter != PatientTimelineEventTypes.MedicalNote)
        {
            return (0, []);
        }

        var query = _db.MedicalNotes
            .AsNoTracking()
            .Where(note => note.PatientId == patientId);

        if (from is { } fromDate)
        {
            query = query.Where(note => note.CreatedAtUtc >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(note => note.CreatedAtUtc <= toDate);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // take <= 0 means count-only (see QueryVisitEventsAsync).
        if (take <= 0)
        {
            return (totalCount, []);
        }

        var rows = await query
            .OrderByDescending(note => note.CreatedAtUtc)
            .ThenBy(note => note.Id)
            .Take(take)
            .Select(note => new { note.Id, note.NoteContent, note.VisitId, note.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return (totalCount, rows
            .Select(note => new TimelineRow(
                note.Id,
                PatientTimelineEventTypes.MedicalNote,
                note.CreatedAtUtc,
                "Clinical Note",
                note.NoteContent,
                note.VisitId))
            .ToList());
    }

    /// <summary>Midnight UTC of a calendar date (the date column carries no time).</summary>
    private static DateTimeOffset ToUtcInstant(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>"Dosage · Frequency" with empty parts skipped; null when both are empty.</summary>
    private static string? DescribeMedication(string dosage, string frequency)
    {
        var details = new[] { dosage, frequency }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return details.Length == 0 ? null : string.Join(" · ", details);
    }

    /// <summary>
    /// Maps the filter input to the canonical contract value. The validator
    /// rejects any other spelling before this point; reaching the fallback
    /// means the request/validation contract drifted and must fail loudly.
    /// </summary>
    private static string? NormalizeEventType(string? eventType)
    {
        if (eventType is null)
        {
            return null;
        }

        return eventType.ToLowerInvariant() switch
        {
            "visit" => PatientTimelineEventTypes.Visit,
            "diagnosis" => PatientTimelineEventTypes.Diagnosis,
            "medication" => PatientTimelineEventTypes.Medication,
            "medicalnote" => PatientTimelineEventTypes.MedicalNote,
            _ => throw new InvalidOperationException($"Unknown timeline event type '{eventType}'.")
        };
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
