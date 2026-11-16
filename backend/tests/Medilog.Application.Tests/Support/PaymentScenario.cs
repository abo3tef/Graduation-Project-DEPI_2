using AutoMapper;
using Medilog.Application.Common;
using Medilog.Application.Features.Payments;
using Medilog.Application.Features.Payments.Requests;
using Medilog.Application.Features.Payments.Responses;
using Medilog.Application.Features.Payments.Validators;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// Two doctors (A, B), each with one patient and one appointment, and one
/// secretary assigned to doctor A only. The current user defaults to the
/// secretary.
/// </summary>
internal sealed class PaymentScenario
{
    public static readonly DateOnly Day = new(2030, 1, 2);

    public static readonly DateTimeOffset Now = new(2030, 1, 2, 10, 0, 0, TimeSpan.Zero);

    public PaymentScenario()
    {
        DoctorA = new Doctor { FullName = "Doctor A", AccountStatus = AccountStatus.Active };
        DoctorB = new Doctor { FullName = "Doctor B", AccountStatus = AccountStatus.Active };
        Secretary = new Secretary { FullName = "Secretary", AccountStatus = AccountStatus.Active };
        PatientA = new Patient { FullName = "Patient A", Phone = "0100", DoctorId = DoctorA.Id };
        PatientB = new Patient { FullName = "Patient B", Phone = "0111", DoctorId = DoctorB.Id };
        AppointmentA = SeedAppointment(DoctorA, PatientA, "10:00", "10:30");
        AppointmentB = SeedAppointment(DoctorB, PatientB, "11:00", "11:30");

        Store.Doctors.AddRange([DoctorA, DoctorB]);
        Store.Secretaries.Add(Secretary);
        Store.Patients.AddRange([PatientA, PatientB]);
        Store.Assignments[Secretary.Id] = [DoctorA.Id];

        User = new FakeCurrentUser { UserId = Secretary.Id, Role = UserRole.Secretary };

        var mapper = new MapperConfiguration(
            configuration => configuration.AddProfile<PaymentsMappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();

        Service = new PaymentService(
            Store,
            User,
            mapper,
            Clock,
            new CreatePaymentRequestValidator(),
            new UpdatePaymentRequestValidator(),
            new UpdatePaymentStatusRequestValidator(),
            new ListPaymentsQueryValidator(),
            new PayableAppointmentsQueryValidator());
    }

    public Doctor DoctorA { get; }

    public Doctor DoctorB { get; }

    public Secretary Secretary { get; }

    public Patient PatientA { get; }

    public Patient PatientB { get; }

    public Appointment AppointmentA { get; }

    public Appointment AppointmentB { get; }

    public FakePaymentStore Store { get; } = new();

    public FakeTimeProvider Clock { get; } = new();

    public FakeCurrentUser User { get; }

    public PaymentService Service { get; }

    public void ActAsSecretary() => (User.UserId, User.Role) = (Secretary.Id, UserRole.Secretary);

    public void ActAsDoctor(Doctor doctor) => (User.UserId, User.Role) = (doctor.Id, UserRole.Doctor);

    public void AssignSecretaryTo(Doctor doctor) => Store.Assignments[Secretary.Id].Add(doctor.Id);

    public Task<PaymentResponse> CreateAsync(
        Guid? appointmentId = null,
        decimal amount = 500m,
        PaymentMethod method = PaymentMethod.Cash,
        string? notes = null) =>
        Service.CreateAsync(
            new CreatePaymentRequest(appointmentId ?? AppointmentA.Id, amount, method, notes),
            CancellationToken.None);

    /// <summary>Inserts a payment directly (bypassing the service rules).</summary>
    public Payment Seed(
        Doctor doctor,
        Patient patient,
        Appointment appointment,
        decimal amount = 500m,
        PaymentMethod method = PaymentMethod.Cash,
        PaymentStatus status = PaymentStatus.Pending,
        string? notes = null,
        DateTimeOffset? paidAt = null)
    {
        var payment = new Payment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentId = appointment.Id,
            CreatedBy = Secretary.Id,
            Amount = amount,
            Method = method,
            Status = status,
            PaidAtUtc = paidAt,
            Notes = notes
        };

        Store.Payments.Add(payment);
        return payment;
    }

    /// <summary>
    /// Adds another appointment to the in-memory store, optionally on a different
    /// date, so date filters and payable lists can be exercised.
    /// </summary>
    public Appointment AddAppointment(
        Doctor doctor,
        Patient patient,
        DateOnly date,
        string start = "12:00",
        string end = "12:30",
        AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            CreatedBy = doctor.Id,
            AppointmentDate = date,
            StartTime = TimeOnly.Parse(start),
            EndTime = TimeOnly.Parse(end),
            Status = status
        };

        Store.Appointments.Add(appointment);
        return appointment;
    }

    private Appointment SeedAppointment(Doctor doctor, Patient patient, string start, string end) =>
        AddAppointment(doctor, patient, Day, start, end);
}

internal sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = PaymentScenario.Now;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
