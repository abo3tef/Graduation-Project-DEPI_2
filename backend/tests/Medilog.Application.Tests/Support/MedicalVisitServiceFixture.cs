using AutoMapper;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Validation;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// Builds the Application service on top of a throwaway EF Core InMemory database.
/// </summary>
/// <remarks>
/// The InMemory provider is the approved test provider for the F4-C2a service tests
/// (decision D-15). It does not enforce relational constraints such as the unique appointment
/// index or the composite foreign keys, so those rules are asserted through application behaviour
/// here and remain unverified against a real MySQL instance.
/// </remarks>
internal sealed class MedicalVisitServiceFixture : IDisposable
{
    private static readonly IMapper Mapper = new MapperConfiguration(
        configuration => configuration.AddProfile<MedicalRecordsMappingProfile>(),
        NullLoggerFactory.Instance)
        .CreateMapper();

    public MedicalVisitServiceFixture(Guid? actingDoctorId, string? role = Roles.Doctor)
    {
        DbContext = CreateContext();
        CurrentUser = new StubCurrentUser { UserId = actingDoctorId, Role = role };

        Service = new MedicalVisitService(
            DbContext,
            CurrentUser,
            Mapper,
            new CreateMedicalVisitRequestValidator(),
            new UpdateMedicalVisitRequestValidator(),
            new CreateDiagnosisRequestValidator(),
            new UpdateDiagnosisRequestValidator());
    }

    public MedilogDbContext DbContext { get; }

    public StubCurrentUser CurrentUser { get; }

    public IMedicalVisitService Service { get; }

    public Task<int> SaveChangesAsync() => DbContext.SaveChangesAsync();

    public void Dispose() => DbContext.Dispose();

    private static MedilogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MedilogDbContext>()
            .UseInMemoryDatabase($"medilog-f4c2-{Guid.NewGuid():N}")
            .Options;

        return new MedilogDbContext(options);
    }
}

/// <summary>A controllable <see cref="ICurrentUser"/> standing in for the WebAPI implementation.</summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public Guid? UserId { get; init; }

    public string? Role { get; init; }
}

/// <summary>
/// Minimal valid entities for the medical-records tests. Values are synthetic; no real patient
/// data is used (AGENTS.md §5).
/// </summary>
internal static class TestData
{
    public static Doctor Doctor() => new()
    {
        FullName = "Doctor Under Test",
        Email = $"{Guid.NewGuid():N}@medilog.test",
        PasswordHash = "test-only-placeholder",
        AccountStatus = AccountStatus.Active,
    };

    public static Patient Patient(Guid? owningDoctorId) => new()
    {
        DoctorId = owningDoctorId,
        FullName = "Patient Under Test",
        DateOfBirth = new DateOnly(1990, 4, 12),
        Gender = Gender.Female,
    };

    public static Appointment Appointment(Guid patientId, Guid doctorId) => new()
    {
        PatientId = patientId,
        DoctorId = doctorId,
        CreatedBy = doctorId,
        AppointmentDate = new DateOnly(2026, 11, 10),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(9, 30),
        Status = AppointmentStatus.Scheduled,
    };

    public static MedicalVisit Visit(Guid patientId, Guid doctorId) => new()
    {
        PatientId = patientId,
        DoctorId = doctorId,
        VisitDate = DateTimeOffset.UtcNow.AddDays(-1),
        ChiefComplaint = "Original complaint",
        Symptoms = "Original symptoms",
        ClinicalNotes = "Original notes",
    };

    public static Diagnosis Diagnosis(MedicalVisit visit, string name, DateOnly date) => new()
    {
        VisitId = visit.Id,
        PatientId = visit.PatientId,
        DiagnosisName = name,
        Description = $"{name} description",
        DiagnosisDate = date,
    };
}
