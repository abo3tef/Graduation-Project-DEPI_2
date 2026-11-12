using Medilog.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Medilog.Infrastructure.Persistence.Appointments;

/// <summary>
/// Wraps the EF Core transaction that holds the per-doctor lock. Disposing it
/// without committing rolls the transaction back and releases the lock.
/// </summary>
internal sealed class AppointmentTransaction : IAppointmentTransaction
{
    private readonly IDbContextTransaction _transaction;

    public AppointmentTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken cancellationToken) =>
        _transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
