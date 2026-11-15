using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Requests;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Application.Features.MedicalRecords.Validators;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// Two active doctors (A, B), one patient owned by each, one unowned patient,
/// and one secretary. The current user defaults to the secretary so that tests
/// must switch to a doctor explicitly.
/// </summary>
internal sealed class MedicalRecordScenario
{
    /// <summary>
    /// With <paramref name="countQueries"/>, every EF Core query compilation is
    /// counted in <see cref="QueryCounting"/>, so tests can assert the exact
    /// query shape of a use case (e.g. that a count-only pass runs no fetch
    /// queries).
    /// </summary>
    public MedicalRecordScenario(bool countQueries = false)
    {
        DoctorA = new Doctor { FullName = "Doctor A", AccountStatus = AccountStatus.Active };
        DoctorB = new Doctor { FullName = "Doctor B", AccountStatus = AccountStatus.Active };
        Secretary = new Secretary { FullName = "Secretary", AccountStatus = AccountStatus.Active };

        PatientA = new Patient
        {
            FullName = "Patient A",
            DateOfBirth = new DateOnly(2010, 6, 15),
            Gender = Gender.Male,
            Phone = "0100",
            Address = "Cairo",
            BloodType = BloodType.APositive,
            EmergencyContact = "0122-9999",
            DoctorId = DoctorA.Id
        };

        PatientB = new Patient
        {
            FullName = "Patient B",
            DateOfBirth = new DateOnly(2000, 2, 29),
            Gender = Gender.Female,
            DoctorId = DoctorB.Id
        };

        UnownedPatient = new Patient
        {
            FullName = "Unowned Patient",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Male
        };

        // A Doctor A-owned patient with a Feb 29 birth date, for age-derivation tests.
        LeapPatient = new Patient
        {
            FullName = "Leap Patient",
            DateOfBirth = new DateOnly(2000, 2, 29),
            Gender = Gender.Female,
            DoctorId = DoctorA.Id
        };

        Store.Doctors.AddRange([DoctorA, DoctorB]);
        Store.Patients.AddRange([PatientA, PatientB, UnownedPatient, LeapPatient]);

        User = new FakeCurrentUser { UserId = Secretary.Id, Role = UserRole.Secretary };

        if (countQueries)
        {
            QueryCounting = new QueryCountingInterceptor();
        }

        Db = CreateDbContext(countQueries);

        Service = new MedicalRecordService(
            Store,
            User,
            Clock,
            new GetMedicalProfileRequestValidator(),
            new GetMedicalHistoryRequestValidator(),
            Db,
            new GetPatientTimelineRequestValidator());
    }

    public Doctor DoctorA { get; }

    public Doctor DoctorB { get; }

    public Secretary Secretary { get; }

    public Patient PatientA { get; }

    public Patient PatientB { get; }

    public Patient UnownedPatient { get; }

    public Patient LeapPatient { get; }

    public FakeMedicalRecordStore Store { get; } = new();

    /// <summary>
    /// The timeline data source: a real InMemory <see cref="MedilogDbContext"/>
    /// (decision D-15 pattern), so ordering, filters and paging run through EF
    /// Core exactly as in production. Authorization still resolves through the
    /// fake store above, matching the service's wiring.
    /// </summary>
    public MedilogDbContext Db { get; }

    public FakeClock Clock { get; } = new();

    public FakeCurrentUser User { get; }

    /// <summary>
    /// Non-null only when the scenario was constructed with
    /// <c>countQueries: true</c>; counts every compiled query on <see cref="Db"/>.
    /// </summary>
    public QueryCountingInterceptor? QueryCounting { get; }

    public MedicalRecordService Service { get; }

    public void ActAsDoctor(Doctor doctor) => (User.UserId, User.Role) = (doctor.Id, UserRole.Doctor);

    public void ActAsSecretary() => (User.UserId, User.Role) = (Secretary.Id, UserRole.Secretary);

    public Task<PatientMedicalProfileResponse> ProfileAsync(Patient? patient = null) =>
        Service.GetMedicalProfileAsync(
            new GetMedicalProfileRequest((patient ?? PatientA).Id),
            CancellationToken.None);

    public Task<PagedResult<MedicalVisitResponse>> HistoryAsync(
        Patient? patient = null,
        int page = 1,
        int pageSize = PagingDefaults.DefaultPageSize) =>
        Service.GetMedicalHistoryAsync(
            new GetMedicalHistoryRequest((patient ?? PatientA).Id, page, pageSize),
            CancellationToken.None);

    /// <summary>
    /// Runs the timeline use case for one patient. Pending seed rows are
    /// flushed first so a test only needs to seed and call.
    /// </summary>
    public async Task<PagedResult<PatientTimelineEventResponse>> TimelineAsync(
        Patient? patient = null,
        string? eventType = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = PagingDefaults.DefaultPageSize)
    {
        await Db.SaveChangesAsync();

        return await Service.GetPatientTimelineAsync(
            new GetPatientTimelineRequest((patient ?? PatientA).Id, eventType, from, to, page, pageSize),
            CancellationToken.None);
    }

    /// <summary>Inserts a visit directly (bypassing all rules).</summary>
    public MedicalVisit SeedVisit(
        Patient patient,
        Doctor doctor,
        DateTimeOffset date,
        string? chiefComplaint = null)
    {
        var visit = new MedicalVisit
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            VisitDate = date,
            ChiefComplaint = chiefComplaint
        };

        Store.Visits.Add(visit);
        return visit;
    }

    /// <summary>Seeds a visit into the timeline database (the store's Visits list is not used there).</summary>
    public MedicalVisit SeedDbVisit(
        Patient patient,
        DateTimeOffset date,
        string? chiefComplaint = null,
        Guid? id = null)
    {
        var visit = new MedicalVisit
        {
            Id = id ?? Guid.NewGuid(),
            PatientId = patient.Id,
            DoctorId = DoctorA.Id,
            VisitDate = date,
            ChiefComplaint = chiefComplaint
        };

        Db.MedicalVisits.Add(visit);
        return visit;
    }

    public Diagnosis SeedDbDiagnosis(MedicalVisit visit, string name, DateOnly date, Guid? id = null)
    {
        var diagnosis = new Diagnosis
        {
            Id = id ?? Guid.NewGuid(),
            VisitId = visit.Id,
            PatientId = visit.PatientId,
            DiagnosisName = name,
            Description = $"{name} description",
            DiagnosisDate = date
        };

        Db.Diagnoses.Add(diagnosis);
        return diagnosis;
    }

    public Medication SeedDbMedication(
        MedicalVisit visit,
        string name = "Amoxicillin",
        string dosage = "500 mg",
        string frequency = "Every 8 hours",
        Guid? id = null)
    {
        var medication = new Medication
        {
            Id = id ?? Guid.NewGuid(),
            VisitId = visit.Id,
            PatientId = visit.PatientId,
            MedicationName = name,
            Dosage = dosage,
            Frequency = frequency,
            Duration = "7 days",
            Instructions = "Take after food"
        };

        Db.Medications.Add(medication);
        return medication;
    }

    public MedicalNote SeedDbNote(Patient patient, Guid? visitId, string content, Guid? id = null)
    {
        var note = new MedicalNote
        {
            Id = id ?? Guid.NewGuid(),
            PatientId = patient.Id,
            DoctorId = DoctorA.Id,
            VisitId = visitId,
            NoteContent = content
        };

        Db.MedicalNotes.Add(note);
        return note;
    }

    private MedilogDbContext CreateDbContext(bool countQueries)
    {
        var options = new DbContextOptionsBuilder<MedilogDbContext>()
            .UseInMemoryDatabase($"medilog-f10c1-{Guid.NewGuid():N}")
            .Options;

        if (countQueries)
        {
            options = new DbContextOptionsBuilder<MedilogDbContext>(options)
                .AddInterceptors(QueryCounting!)
                .Options;
        }

        return new MedilogDbContext(options);
    }
}
