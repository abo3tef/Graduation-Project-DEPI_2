using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Medilog.Infrastructure.Persistence;

/// <summary>
/// Adapts the Oracle MySQL provider to a MariaDB server used for local development.
/// </summary>
/// <remarks>
/// <para>
/// MariaDB and MySQL 8 differ in two places that break <c>MigrateAsync</c> with
/// <c>MySql.EntityFrameworkCore</c> 10.x:
/// </para>
/// <list type="bullet">
/// <item>
/// The provider acquires the EF migration lock with <c>GET_LOCK(name, -1)</c> and unboxes the scalar
/// directly to <see cref="long"/>. MariaDB returns <c>NULL</c> for a negative timeout (and an
/// <see cref="int"/> for a successful lock), so the unbox throws
/// <c>InvalidCastException: DBNull to Int64</c>. The timeout is rewritten to a bounded wait and the
/// result is normalized to <see cref="long"/> before the provider sees it.
/// </item>
/// <item>
/// The model uses the MySQL-8-only collation <c>utf8mb4_0900_ai_ci</c>; MariaDB rejects it. It is
/// replaced with <c>utf8mb4_unicode_ci</c>, which both engines support.
/// </item>
/// </list>
/// <para>
/// Every rewrite is guarded by <see cref="DbConnection.ServerVersion"/>, so a real MySQL 8 server
/// sees the original commands and behavior. This is a host-level compatibility shim: no entity
/// configuration, migration or package version is changed.
/// </para>
/// </remarks>
public sealed class MariaDbCompatibilityInterceptor : DbCommandInterceptor
{
    private const string MigrationLock = "GET_LOCK('__EFMigrationsLock'";
    private const string InfiniteWaitLock = "GET_LOCK('__EFMigrationsLock',-1)";
    private const string BoundedWaitLock = "GET_LOCK('__EFMigrationsLock',30)";
    private const string MySqlEightCollation = "utf8mb4_0900_ai_ci";
    private const string PortableCollation = "utf8mb4_unicode_ci";

    private static bool IsMariaDb(DbCommand command) =>
        command.Connection?.ServerVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) is true;

    private static bool IsMigrationLock(DbCommand command) =>
        command.CommandText.Contains(MigrationLock, StringComparison.OrdinalIgnoreCase);

    private static void Adapt(DbCommand command)
    {
        if (!IsMariaDb(command))
        {
            return;
        }

        if (command.CommandText.Contains(InfiniteWaitLock, StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = command.CommandText.Replace(
                InfiniteWaitLock, BoundedWaitLock, StringComparison.OrdinalIgnoreCase);
        }

        if (command.CommandText.Contains(MySqlEightCollation, StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = command.CommandText.Replace(
                MySqlEightCollation, PortableCollation, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static object? NormalizeLockResult(object? value) => value switch
    {
        long seconds => seconds,
        int seconds => (long)seconds,
        _ => 0L,
    };

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Adapt(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken)
    {
        Adapt(command);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
        => IsMariaDb(command) && IsMigrationLock(command)
            ? NormalizeLockResult(result)
            : result;

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(
            IsMariaDb(command) && IsMigrationLock(command) ? NormalizeLockResult(result) : result);

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Adapt(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken)
    {
        Adapt(command);
        return ValueTask.FromResult(result);
    }
}
