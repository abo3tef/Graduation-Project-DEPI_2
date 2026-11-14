using Medilog.Application.Common.Paging;
using Medilog.Application.Interfaces;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Infrastructure.Persistence.MedicalRecords;

/// <summary>
/// EF Core implementation of the Application's <see cref="IMedicalRecordStore"/>.
/// Read-only: every query is <c>AsNoTracking</c> with an explicit projection to
/// the minimum columns. The visit page runs in the database (ordering is required
/// for correct paging); children are loaded per page by their visit ids and,
/// where the column exists, also by the patient id, so records can never cross
/// patients. Child collections are returned unsorted — the Application service
/// applies the deterministic response ordering (string-stored enums cannot be
/// ranked in SQL).
/// </summary>
internal sealed class MedicalRecordStore : IMedicalRecordStore
{
    private readonly MedilogDbContext _db;

    public MedicalRecordStore(MedilogDbContext db)
    {
        _db = db;
    }

    public Task<MedicalDoctorInfo?> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        _db.Doctors
            .AsNoTracking()
            .Where(doctor => doctor.Id == doctorId)
            .Select(doctor => new MedicalDoctorInfo(doctor.Id, doctor.AccountStatus == AccountStatus.Active))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PatientOwnerInfo?> GetPatientOwnerAsync(Guid patientId, CancellationToken cancellationToken) =>
        _db.Patients
            .AsNoTracking()
            .Where(patient => patient.Id == patientId)
            .Select(patient => new PatientOwnerInfo(patient.Id, patient.DoctorId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PatientProfileData?> GetProfileDataAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var patient = await _db.Patients
            .AsNoTracking()
            .Where(candidate => candidate.Id == patientId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.FullName,
                candidate.DateOfBirth,
                candidate.Gender,
                candidate.Phone,
                candidate.Address,
                candidate.BloodType,
                candidate.EmergencyContact
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
        {
            return null;
        }

        var allergies = await _db.Allergies
            .AsNoTracking()
            .Where(allergy => allergy.PatientId == patientId)
            .Select(allergy => new PatientAllergyData(
                allergy.Id,
                allergy.AllergyName,
                allergy.Severity,
                allergy.Description,
                allergy.Notes))
            .ToListAsync(cancellationToken);

        var conditions = await _db.ChronicConditions
            .AsNoTracking()
            .Where(condition => condition.PatientId == patientId)
            .Select(condition => new PatientConditionData(
                condition.Id,
                condition.ConditionName,
                condition.DiagnosisDate,
                condition.Status,
                condition.Notes))
            .ToListAsync(cancellationToken);

        return new PatientProfileData(
            patient.Id,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender,
            patient.Phone,
            patient.Address,
            patient.BloodType,
            patient.EmergencyContact,
            allergies,
            conditions);
    }

    public async Task<PagedResult<MedicalVisitData>> ListVisitsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.MedicalVisits
            .AsNoTracking()
            .Where(visit => visit.PatientId == patientId);

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (pageNumber - 1) * pageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<MedicalVisitData>([], pageNumber, pageSize, totalCount);
        }

        // Deterministic order: the id is a stable tie-breaker for equal instants.
        var headers = await query
            .OrderByDescending(visit => visit.VisitDate)
            .ThenByDescending(visit => visit.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(visit => new VisitHeaderData(
                visit.Id,
                visit.VisitDate,
                visit.DoctorId,
                visit.Doctor.FullName,
                visit.ChiefComplaint,
                visit.Symptoms,
                visit.ClinicalNotes))
            .ToListAsync(cancellationToken);

        var visitIds = headers.Select(header => header.VisitId).ToArray();

        // Every child is loaded by visit id; where the table also carries a
        // patient column it is filtered as well. VitalSigns has no patient
        // column: the visit ids are already patient-scoped, so the rows cannot
        // leak across patients.

        var diagnoses = await _db.Diagnoses
            .AsNoTracking()
            .Where(diagnosis => diagnosis.PatientId == patientId && visitIds.Contains(diagnosis.VisitId))
            .Select(diagnosis => new VisitDiagnosisData(
                diagnosis.VisitId,
                diagnosis.Id,
                diagnosis.DiagnosisName,
                diagnosis.Description,
                diagnosis.DiagnosisDate))
            .ToListAsync(cancellationToken);

        var medications = await _db.Medications
            .AsNoTracking()
            .Where(medication => medication.PatientId == patientId && visitIds.Contains(medication.VisitId))
            .Select(medication => new VisitMedicationData(
                medication.VisitId,
                medication.Id,
                medication.MedicationName,
                medication.Dosage,
                medication.Frequency,
                medication.Duration,
                medication.Instructions))
            .ToListAsync(cancellationToken);

        var vitalSigns = await _db.VitalSigns
            .AsNoTracking()
            .Where(vitalSigns => visitIds.Contains(vitalSigns.VisitId))
            .Select(vitalSigns => new VisitVitalSignsData(
                vitalSigns.VisitId,
                vitalSigns.Id,
                vitalSigns.BloodPressure,
                vitalSigns.HeartRate,
                vitalSigns.Temperature,
                vitalSigns.RespiratoryRate,
                vitalSigns.Weight,
                vitalSigns.Height,
                vitalSigns.SpO2))
            .ToListAsync(cancellationToken);

        // Only visit-linked rows are in F3-C1 scope (task file D-11); rows with
        // a NULL visit id are deferred to F3-C2 and are never loaded here.

        var notes = await _db.MedicalNotes
            .AsNoTracking()
            .Where(note =>
                note.PatientId == patientId
                && note.VisitId != null
                && visitIds.Contains(note.VisitId.Value))
            .Select(note => new VisitNoteData(
                note.VisitId!.Value,
                note.Id,
                note.DoctorId,
                note.Doctor.FullName,
                note.NoteContent,
                note.CreatedAtUtc,
                note.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var labResults = await _db.LaboratoryResults
            .AsNoTracking()
            .Where(labResult =>
                labResult.PatientId == patientId
                && labResult.VisitId != null
                && visitIds.Contains(labResult.VisitId.Value))
            .Select(labResult => new VisitLabResultData(
                labResult.VisitId!.Value,
                labResult.Id,
                labResult.TestName,
                labResult.TestDate,
                labResult.Value,
                labResult.Unit,
                labResult.ReferenceRange,
                labResult.Notes))
            .ToListAsync(cancellationToken);

        var items = headers
            .Select(header => new MedicalVisitData(
                header.VisitId,
                header.VisitDate,
                header.DoctorId,
                header.DoctorName,
                header.ChiefComplaint,
                header.Symptoms,
                header.ClinicalNotes,
                diagnoses.Where(diagnosis => diagnosis.VisitId == header.VisitId).ToArray(),
                medications.Where(medication => medication.VisitId == header.VisitId).ToArray(),
                vitalSigns.Where(vitalSigns => vitalSigns.VisitId == header.VisitId).ToArray(),
                notes.Where(note => note.VisitId == header.VisitId).ToArray(),
                labResults.Where(labResult => labResult.VisitId == header.VisitId).ToArray()))
            .ToArray();

        return new PagedResult<MedicalVisitData>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResult<PatientNoteData>> ListNotesAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Every note of the patient (F3-C3a C-2), linked and unlinked. The
        // doctor join supplies the display name without loading the graph.
        var query = _db.MedicalNotes
            .AsNoTracking()
            .Where(note => note.PatientId == patientId);

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (pageNumber - 1) * pageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<PatientNoteData>([], pageNumber, pageSize, totalCount);
        }

        var items = await query
            .OrderByDescending(note => note.CreatedAtUtc)
            .ThenByDescending(note => note.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(note => new PatientNoteData(
                note.Id,
                note.VisitId,
                note.DoctorId,
                note.Doctor.FullName,
                note.NoteContent,
                note.CreatedAtUtc,
                note.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<PatientNoteData>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResult<PatientLabResultData>> ListLabResultsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.LaboratoryResults
            .AsNoTracking()
            .Where(labResult => labResult.PatientId == patientId);

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (pageNumber - 1) * pageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<PatientLabResultData>([], pageNumber, pageSize, totalCount);
        }

        var items = await query
            .OrderByDescending(labResult => labResult.TestDate)
            .ThenByDescending(labResult => labResult.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(labResult => new PatientLabResultData(
                labResult.Id,
                labResult.VisitId,
                labResult.TestName,
                labResult.TestDate,
                labResult.Value,
                labResult.Unit,
                labResult.ReferenceRange,
                labResult.Notes))
            .ToListAsync(cancellationToken);

        return new PagedResult<PatientLabResultData>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResult<PatientTimelineEventData>> ListTimelineAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // F3-C3b Q-12: one feed over four event sources. The sources live in
        // different tables with different date columns (TestDate is a DATE, the
        // others are instants), so a single portable SQL union order is not
        // available. The composition is therefore exact-but-bounded: the
        // database orders and limits each source independently (every fetch
        // below is a column-projected, SQL-ordered top-(skip + pageSize)
        // query), and the global merge over at most 4 × (skip + pageSize)
        // materialized rows happens here. No source is ever loaded beyond its
        // page prefix (AGENTS §3.1 — the heavy filtering/ordering stays in SQL).
        var visitCount = await _db.MedicalVisits
            .AsNoTracking()
            .CountAsync(visit => visit.PatientId == patientId, cancellationToken);
        var noteCount = await _db.MedicalNotes
            .AsNoTracking()
            .CountAsync(note => note.PatientId == patientId, cancellationToken);
        var labResultCount = await _db.LaboratoryResults
            .AsNoTracking()
            .CountAsync(labResult => labResult.PatientId == patientId, cancellationToken);
        var documentCount = await _db.MedicalDocuments
            .AsNoTracking()
            .CountAsync(document => document.PatientId == patientId, cancellationToken);

        var totalCount = visitCount + noteCount + labResultCount + documentCount;
        var skip = (pageNumber - 1) * pageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<PatientTimelineEventData>([], pageNumber, pageSize, totalCount);
        }

        var take = skip + pageSize;

        var visits = await _db.MedicalVisits
            .AsNoTracking()
            .Where(visit => visit.PatientId == patientId)
            .OrderByDescending(visit => visit.VisitDate)
            .ThenByDescending(visit => visit.Id)
            .Take(take)
            .Select(visit => new PatientTimelineEventData(
                visit.Id,
                TimelineEventTypes.Visit,
                visit.VisitDate,
                visit.Id,
                visit.DoctorId,
                visit.Doctor.FullName,
                visit.ChiefComplaint,
                // Child counts are per-visit and patient-scoped, mirroring the
                // defensive child filters used by the visit history query.
                visit.Diagnoses.Count(diagnosis => diagnosis.PatientId == patientId),
                visit.Medications.Count(medication => medication.PatientId == patientId),
                visit.MedicalNotes.Count(note => note.PatientId == patientId),
                visit.LaboratoryResults.Count(labResult => labResult.PatientId == patientId),
                null,
                null,
                null,
                null,
                null,
                null,
                null))
            .ToListAsync(cancellationToken);

        var notes = await _db.MedicalNotes
            .AsNoTracking()
            .Where(note => note.PatientId == patientId)
            .OrderByDescending(note => note.CreatedAtUtc)
            .ThenByDescending(note => note.Id)
            .Take(take)
            .Select(note => new PatientTimelineEventData(
                note.Id,
                TimelineEventTypes.Note,
                note.CreatedAtUtc,
                note.VisitId,
                note.DoctorId,
                note.Doctor.FullName,
                null,
                0,
                0,
                0,
                0,
                note.NoteContent,
                null,
                null,
                null,
                null,
                null,
                null))
            .ToListAsync(cancellationToken);

        // LaboratoryResult.TestDate is a DateOnly column; the projection stays
        // column-direct (portable SQL) and the midnight-UTC instant is computed
        // in CLR below, after materialization.
        var labRows = await _db.LaboratoryResults
            .AsNoTracking()
            .Where(labResult => labResult.PatientId == patientId)
            .OrderByDescending(labResult => labResult.TestDate)
            .ThenByDescending(labResult => labResult.Id)
            .Take(take)
            .Select(labResult => new
            {
                labResult.Id,
                labResult.VisitId,
                labResult.TestName,
                labResult.TestDate,
                labResult.Value,
                labResult.Unit
            })
            .ToListAsync(cancellationToken);

        var labResults = labRows
            .Select(row => new PatientTimelineEventData(
                row.Id,
                TimelineEventTypes.LabResult,
                new DateTimeOffset(row.TestDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                row.VisitId,
                null,
                null,
                null,
                0,
                0,
                0,
                0,
                null,
                row.TestName,
                row.Value,
                row.Unit,
                null,
                null,
                null))
            .ToArray();

        var documents = await _db.MedicalDocuments
            .AsNoTracking()
            .Where(document => document.PatientId == patientId)
            .OrderByDescending(document => document.UploadDate)
            .ThenByDescending(document => document.Id)
            .Take(take)
            // Q-13: the location column is never projected, so it cannot reach
            // any response (enforced by shape, not by serialization filtering).
            .Select(document => new PatientDocumentData(
                document.Id,
                document.FileName,
                document.Type,
                document.UploadDate,
                document.Description,
                document.UploadedBy,
                document.UploadedByDoctor != null ? document.UploadedByDoctor.FullName : null))
            .ToListAsync(cancellationToken);

        var documentEvents = documents
            .Select(document => new PatientTimelineEventData(
                document.Id,
                TimelineEventTypes.Document,
                document.UploadDateUtc,
                null,
                document.UploadedByDoctorId,
                document.UploadedByDoctorName,
                null,
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null,
                document.FileName,
                document.Type,
                document.Description))
            .ToArray();

        var items = visits
            .Concat(notes)
            .Concat(labResults)
            .Concat(documentEvents)
            .OrderByDescending(@event => @event.OccurredAtUtc)
            .ThenBy(@event => @event.EventType, StringComparer.Ordinal)
            .ThenBy(@event => @event.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToArray();

        return new PagedResult<PatientTimelineEventData>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResult<PatientDocumentData>> ListDocumentsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.MedicalDocuments
            .AsNoTracking()
            .Where(document => document.PatientId == patientId);

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (pageNumber - 1) * pageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<PatientDocumentData>([], pageNumber, pageSize, totalCount);
        }

        var items = await query
            .OrderByDescending(document => document.UploadDate)
            .ThenByDescending(document => document.Id)
            .Skip(skip)
            .Take(pageSize)
            // Q-13: no location projection — the read model has no such field.
            .Select(document => new PatientDocumentData(
                document.Id,
                document.FileName,
                document.Type,
                document.UploadDate,
                document.Description,
                document.UploadedBy,
                document.UploadedByDoctor != null ? document.UploadedByDoctor.FullName : null))
            .ToListAsync(cancellationToken);

        return new PagedResult<PatientDocumentData>(items, pageNumber, pageSize, totalCount);
    }

    /// <summary>Page row of the visit query, before the children are attached.</summary>
    private sealed record VisitHeaderData(
        Guid VisitId,
        DateTimeOffset VisitDate,
        Guid DoctorId,
        string DoctorName,
        string? ChiefComplaint,
        string? Symptoms,
        string? ClinicalNotes);
}
