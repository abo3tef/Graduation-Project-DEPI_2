using Medilog.Application.Common;
using Medilog.Application.Features.Appointments;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Features.Appointments.Responses;
using Medilog.Application.Features.Appointments.Validators;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// Two doctors (A, B), each with one patient, and one secretary assigned to
/// doctor A only. The current user defaults to the secretary.
/// </summary>
internal sealed class Scenario
{
    public static readonly DateOnly Day = new(2030, 1, 2);

    public Scenario()
    {
        DoctorA = new Doctor { FullName = "Doctor A", AccountStatus = AccountStatus.Active };
        DoctorB = new Doctor { FullName = "Doctor B", AccountStatus = AccountStatus.Active };
        Secretary = new Secretary { FullName = "Secretary", AccountStatus = AccountStatus.Active };
        PatientA = new Patient { FullName = "Patient A", Phone = "0100", DoctorId = DoctorA.Id };
        PatientB = new Patient { FullName = "Patient B", Phone = "0111", DoctorId = DoctorB.Id };

        Store.Doctors.AddRange([DoctorA, DoctorB]);
        Store.Secretaries.Add(Secretary);
        Store.Patients.AddRange([PatientA, PatientB]);
        Store.Assignments[Secretary.Id] = [DoctorA.Id];

        User = new FakeCurrentUser { UserId = Secretary.Id, Role = UserRole.Secretary };

        Service = new AppointmentService(
            Store,
            User,
            Clock,
            new BookAppointmentRequestValidator(),
            new RescheduleAppointmentRequestValidator(),
            new CancelAppointmentRequestValidator(),
            new UpdateAppointmentStatusRequestValidator(),
            new ListAppointmentsQueryValidator(),
            new PatientLookupQueryValidator(),
            NullLogger<AppointmentService>.Instance);
    }

    public Doctor DoctorA { get; }

    public Doctor DoctorB { get; }

    public Secretary Secretary { get; }

    public Patient PatientA { get; }

    public Patient PatientB { get; }

    public FakeAppointmentStore Store { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeCurrentUser User { get; }

    public AppointmentService Service { get; }

    public static TimeOnly T(string value) => TimeOnly.Parse(value);

    public void ActAsSecretary() => (User.UserId, User.Role) = (Secretary.Id, UserRole.Secretary);

    public void ActAsDoctor(Doctor doctor) => (User.UserId, User.Role) = (doctor.Id, UserRole.Doctor);

    public void AssignSecretaryTo(Doctor doctor) => Store.Assignments[Secretary.Id].Add(doctor.Id);

    /// <summary>Inserts an appointment directly (bypassing the service rules).</summary>
    public Appointment Seed(
        Doctor doctor,
        Patient patient,
        string start,
        string? end,
        AppointmentStatus status = AppointmentStatus.Scheduled,
        DateOnly? date = null)
    {
        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            CreatedBy = Secretary.Id,
            AppointmentDate = date ?? Day,
            StartTime = T(start),
            EndTime = end is null ? null : T(end),
            Status = status
        };

        Store.Appointments.Add(appointment);
        return appointment;
    }

    public Task<AppointmentResponse> BookAsync(
        string start,
        string? end,
        Doctor? doctor = null,
        Patient? patient = null,
        DateOnly? date = null) =>
        Service.BookAsync(
            new BookAppointmentRequest(
                (patient ?? PatientA).Id,
                (doctor ?? DoctorA).Id,
                date ?? Day,
                T(start),
                end is null ? null : T(end),
                "checkup"),
            CancellationToken.None);
}
