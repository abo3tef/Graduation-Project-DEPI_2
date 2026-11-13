using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Requests;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Application.Features.MedicalRecords.Validators;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// Two active doctors (A, B), one patient owned by each, one unowned patient,
/// and one secretary. The current user defaults to the secretary so that tests
/// must switch to a doctor explicitly.
/// </summary>
internal sealed class MedicalRecordScenario
{
    public MedicalRecordScenario()
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

        Service = new MedicalRecordService(
            Store,
            User,
            Clock,
            new GetMedicalProfileRequestValidator(),
            new GetMedicalHistoryRequestValidator());
    }

    public Doctor DoctorA { get; }

    public Doctor DoctorB { get; }

    public Secretary Secretary { get; }

    public Patient PatientA { get; }

    public Patient PatientB { get; }

    public Patient UnownedPatient { get; }

    public Patient LeapPatient { get; }

    public FakeMedicalRecordStore Store { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeCurrentUser User { get; }

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
}
