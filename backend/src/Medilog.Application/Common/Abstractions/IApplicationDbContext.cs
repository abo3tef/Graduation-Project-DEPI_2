using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// The persistence surface the Application layer is allowed to use.
/// </summary>
/// <remarks>
/// Implemented by the Infrastructure <c>MedilogDbContext</c>. Application code depends on this
/// interface only, never on the concrete context (AGENTS.md §4.1), and no repository abstraction
/// is introduced (AGENTS.md §4.1, decision D-09).
/// </remarks>
public interface IApplicationDbContext
{
    DbSet<MedicalVisit> MedicalVisits { get; }

    DbSet<Diagnosis> Diagnoses { get; }

    DbSet<Patient> Patients { get; }

    DbSet<Doctor> Doctors { get; }

    DbSet<Appointment> Appointments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
