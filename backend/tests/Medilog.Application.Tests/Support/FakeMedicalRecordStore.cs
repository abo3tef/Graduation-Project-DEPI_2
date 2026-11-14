using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// In-memory <see cref="IMedicalRecordStore"/>. It mirrors the contract the real
/// store must honor: children are returned in insertion (i.e. unsorted) order so
/// the tests prove that the service applies the deterministic response ordering.
/// <see cref="CallCount"/> lets tests verify that a rejected caller triggers no
/// data access at all.
/// </summary>
internal sealed class FakeMedicalRecordStore : IMedicalRecordStore
{
    public List<Doctor> Doctors { get; } = [];

    public List<Patient> Patients { get; } = [];

    public List<MedicalVisit> Visits { get; } = [];

    public List<Allergy> Allergies { get; } = [];

    public List<ChronicCondition> Conditions { get; } = [];

    public List<Diagnosis> Diagnoses { get; } = [];

    public List<Medication> Medications { get; } = [];

    public List<VitalSigns> VitalSigns { get; } = [];

    public List<MedicalNote> Notes { get; } = [];

    public List<LaboratoryResult> LabResults { get; } = [];

    public List<MedicalDocument> Documents { get; } = [];

    public int CallCount { get; private set; }

    public Task<MedicalDoctorInfo?> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        CallCount++;

        var doctor = Doctors.FirstOrDefault(candidate => candidate.Id == doctorId);

        return Task.FromResult(
            doctor is null
                ? null
                : new MedicalDoctorInfo(doctor.Id, doctor.AccountStatus == AccountStatus.Active));
    }

    public Task<PatientOwnerInfo?> GetPatientOwnerAsync(Guid patientId, CancellationToken cancellationToken)
    {
        CallCount++;

        var patient = Patients.FirstOrDefault(candidate => candidate.Id == patientId);

        return Task.FromResult(
            patient is null ? null : new PatientOwnerInfo(patient.Id, patient.DoctorId));
    }

    public Task<PatientProfileData?> GetProfileDataAsync(Guid patientId, CancellationToken cancellationToken)
    {
        CallCount++;

        var patient = Patients.FirstOrDefault(candidate => candidate.Id == patientId);

        if (patient is null)
        {
            return Task.FromResult<PatientProfileData?>(null);
        }

        return Task.FromResult<PatientProfileData?>(new PatientProfileData(
            patient.Id,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender,
            patient.Phone,
            patient.Address,
            patient.BloodType,
            patient.EmergencyContact,
            Allergies
                .Where(allergy => allergy.PatientId == patientId)
                .Select(allergy => new PatientAllergyData(
                    allergy.Id,
                    allergy.AllergyName,
                    allergy.Severity,
                    allergy.Description,
                    allergy.Notes))
                .ToArray(),
            Conditions
                .Where(condition => condition.PatientId == patientId)
                .Select(condition => new PatientConditionData(
                    condition.Id,
                    condition.ConditionName,
                    condition.DiagnosisDate,
                    condition.Status,
                    condition.Notes))
                .ToArray()));
    }

    public Task<PagedResult<MedicalVisitData>> ListVisitsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        CallCount++;

        // Same ordering contract as the real store: newest first, id tie-breaker.
        var ordered = Visits
            .Where(visit => visit.PatientId == patientId)
            .OrderByDescending(visit => visit.VisitDate)
            .ThenByDescending(visit => visit.Id)
            .ToArray();

        var skip = (pageNumber - 1) * pageSize;
        var page = ordered.Skip(skip).Take(pageSize).ToArray();

        var items = page.Select(visit => new MedicalVisitData(
            visit.Id,
            visit.VisitDate,
            visit.DoctorId,
            Doctors.FirstOrDefault(doctor => doctor.Id == visit.DoctorId)?.FullName ?? string.Empty,
            visit.ChiefComplaint,
            visit.Symptoms,
            visit.ClinicalNotes,
            Diagnoses
                .Where(diagnosis => diagnosis.PatientId == patientId && diagnosis.VisitId == visit.Id)
                .Select(diagnosis => new VisitDiagnosisData(
                    visit.Id,
                    diagnosis.Id,
                    diagnosis.DiagnosisName,
                    diagnosis.Description,
                    diagnosis.DiagnosisDate))
                .ToArray(),
            Medications
                .Where(medication => medication.PatientId == patientId && medication.VisitId == visit.Id)
                .Select(medication => new VisitMedicationData(
                    visit.Id,
                    medication.Id,
                    medication.MedicationName,
                    medication.Dosage,
                    medication.Frequency,
                    medication.Duration,
                    medication.Instructions))
                .ToArray(),
            VitalSigns
                .Where(vitalSigns => vitalSigns.VisitId == visit.Id)
                .Select(vitalSigns => new VisitVitalSignsData(
                    visit.Id,
                    vitalSigns.Id,
                    vitalSigns.BloodPressure,
                    vitalSigns.HeartRate,
                    vitalSigns.Temperature,
                    vitalSigns.RespiratoryRate,
                    vitalSigns.Weight,
                    vitalSigns.Height,
                    vitalSigns.SpO2))
                .ToArray(),
            Notes
                .Where(note => note.PatientId == patientId && note.VisitId == visit.Id)
                .Select(note => new VisitNoteData(
                    visit.Id,
                    note.Id,
                    note.DoctorId,
                    Doctors.FirstOrDefault(doctor => doctor.Id == note.DoctorId)?.FullName ?? string.Empty,
                    note.NoteContent,
                    note.CreatedAtUtc,
                    note.UpdatedAtUtc))
                .ToArray(),
            LabResults
                .Where(labResult => labResult.PatientId == patientId && labResult.VisitId == visit.Id)
                .Select(labResult => new VisitLabResultData(
                    visit.Id,
                    labResult.Id,
                    labResult.TestName,
                    labResult.TestDate,
                    labResult.Value,
                    labResult.Unit,
                    labResult.ReferenceRange,
                    labResult.Notes))
                .ToArray()))
            .ToArray();

        return Task.FromResult(new PagedResult<MedicalVisitData>(items, pageNumber, pageSize, ordered.Length));
    }

    public Task<PagedResult<PatientNoteData>> ListNotesAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        CallCount++;

        // Same ordering contract as the real store: newest first, id tie-breaker.
        var ordered = Notes
            .Where(note => note.PatientId == patientId)
            .OrderByDescending(note => note.CreatedAtUtc)
            .ThenByDescending(note => note.Id)
            .ToArray();

        var skip = (pageNumber - 1) * pageSize;
        var page = ordered.Skip(skip).Take(pageSize).ToArray();

        var items = page
            .Select(note => new PatientNoteData(
                note.Id,
                note.VisitId,
                note.DoctorId,
                Doctors.FirstOrDefault(doctor => doctor.Id == note.DoctorId)?.FullName ?? string.Empty,
                note.NoteContent,
                note.CreatedAtUtc,
                note.UpdatedAtUtc))
            .ToArray();

        return Task.FromResult(new PagedResult<PatientNoteData>(items, pageNumber, pageSize, ordered.Length));
    }

    public Task<PagedResult<PatientLabResultData>> ListLabResultsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        CallCount++;

        var ordered = LabResults
            .Where(labResult => labResult.PatientId == patientId)
            .OrderByDescending(labResult => labResult.TestDate)
            .ThenByDescending(labResult => labResult.Id)
            .ToArray();

        var skip = (pageNumber - 1) * pageSize;
        var page = ordered.Skip(skip).Take(pageSize).ToArray();

        var items = page
            .Select(labResult => new PatientLabResultData(
                labResult.Id,
                labResult.VisitId,
                labResult.TestName,
                labResult.TestDate,
                labResult.Value,
                labResult.Unit,
                labResult.ReferenceRange,
                labResult.Notes))
            .ToArray();

        return Task.FromResult(new PagedResult<PatientLabResultData>(items, pageNumber, pageSize, ordered.Length));
    }

    public Task<PagedResult<PatientTimelineEventData>> ListTimelineAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        CallCount++;

        // Same contract as the real store: one global ordering across the four
        // sources — occurredAtUtc desc, event type ordinal asc, id asc — with
        // lab events dated at midnight UTC of their test date.
        var events = Visits
            .Where(visit => visit.PatientId == patientId)
            .Select(visit => new PatientTimelineEventData(
                visit.Id,
                TimelineEventTypes.Visit,
                visit.VisitDate,
                visit.Id,
                visit.DoctorId,
                Doctors.FirstOrDefault(doctor => doctor.Id == visit.DoctorId)?.FullName ?? string.Empty,
                visit.ChiefComplaint,
                Diagnoses.Count(diagnosis => diagnosis.PatientId == patientId && diagnosis.VisitId == visit.Id),
                Medications.Count(medication => medication.PatientId == patientId && medication.VisitId == visit.Id),
                Notes.Count(note => note.PatientId == patientId && note.VisitId == visit.Id),
                LabResults.Count(labResult => labResult.PatientId == patientId && labResult.VisitId == visit.Id),
                null,
                null,
                null,
                null,
                null,
                null,
                null))
            .Concat(Notes
                .Where(note => note.PatientId == patientId)
                .Select(note => new PatientTimelineEventData(
                    note.Id,
                    TimelineEventTypes.Note,
                    note.CreatedAtUtc,
                    note.VisitId,
                    note.DoctorId,
                    Doctors.FirstOrDefault(doctor => doctor.Id == note.DoctorId)?.FullName ?? string.Empty,
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
                    null)))
            .Concat(LabResults
                .Where(labResult => labResult.PatientId == patientId)
                .Select(labResult => new PatientTimelineEventData(
                    labResult.Id,
                    TimelineEventTypes.LabResult,
                    new DateTimeOffset(labResult.TestDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    labResult.VisitId,
                    null,
                    null,
                    null,
                    0,
                    0,
                    0,
                    0,
                    null,
                    labResult.TestName,
                    labResult.Value,
                    labResult.Unit,
                    null,
                    null,
                    null)))
            .Concat(Documents
                .Where(document => document.PatientId == patientId)
                .Select(document => new PatientTimelineEventData(
                    document.Id,
                    TimelineEventTypes.Document,
                    document.UploadDate,
                    null,
                    document.UploadedBy,
                    Doctors.FirstOrDefault(doctor => doctor.Id == document.UploadedBy)?.FullName,
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
                    document.Description)))
            .OrderByDescending(@event => @event.OccurredAtUtc)
            .ThenBy(@event => @event.EventType, StringComparer.Ordinal)
            .ThenBy(@event => @event.Id)
            .ToArray();

        var skip = (pageNumber - 1) * pageSize;
        var items = events.Skip(skip).Take(pageSize).ToArray();

        return Task.FromResult(new PagedResult<PatientTimelineEventData>(items, pageNumber, pageSize, events.Length));
    }

    public Task<PagedResult<PatientDocumentData>> ListDocumentsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        CallCount++;

        var ordered = Documents
            .Where(document => document.PatientId == patientId)
            .OrderByDescending(document => document.UploadDate)
            .ThenByDescending(document => document.Id)
            .ToArray();

        var skip = (pageNumber - 1) * pageSize;
        var page = ordered.Skip(skip).Take(pageSize).ToArray();

        var items = page
            .Select(document => new PatientDocumentData(
                document.Id,
                document.FileName,
                document.Type,
                document.UploadDate,
                document.Description,
                document.UploadedBy,
                document.UploadedBy is null
                    ? null
                    : Doctors.FirstOrDefault(doctor => doctor.Id == document.UploadedBy)?.FullName))
            .ToArray();

        return Task.FromResult(new PagedResult<PatientDocumentData>(items, pageNumber, pageSize, ordered.Length));
    }
}
