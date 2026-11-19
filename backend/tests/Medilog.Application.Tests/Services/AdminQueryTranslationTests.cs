using Medilog.Application.Common;
using Medilog.Application.Features.Admin;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Tests.Services;

/// <summary>
/// Regression coverage for the relational translation of the admin list query. The bug this guards
/// against only appears on a real provider: projecting the positional <c>AdminUserRecord</c> before
/// composing <c>OrderBy</c> produced "new AdminUserRecord(...).Email could not be translated", while
/// the InMemory provider still returned data and hid the fault.
/// </summary>
public sealed class AdminQueryTranslationTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("Doctor", null, null)]
    [InlineData(null, "Active", null)]
    [InlineData(null, null, "doctor@medilog.test")]
    [InlineData("Doctor", "Active", "doctor@medilog.test")]
    public void List_query_translates_filters_deterministic_order_and_paging_to_sql(
        string? role,
        string? status,
        string? search)
    {
        // No connection is opened: ToQueryString only compiles the query against the provider model.
        var options = new DbContextOptionsBuilder<MedilogDbContext>()
            .UseMySQL("Server=localhost;Database=medilog_query_test;Uid=test;Pwd=test;")
            .Options;

        using var dbContext = new MedilogDbContext(options);

        // Only IApplicationDbContext is used by the query-building methods under test.
        var service = new AdminService(dbContext, null!, null!, null!, null!, null!, null!, null!);

        UserRole? parsedRole = role is null ? null : Enum.Parse<UserRole>(role);
        AccountStatus? parsedStatus = status is null ? null : Enum.Parse<AccountStatus>(status);

        var sql = service
            .BuildListQuery(parsedRole, parsedStatus, search)
            .Skip(0)
            .Take(10)
            .ToQueryString();

        Assert.Contains("UNION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new AdminUserRecord", sql, StringComparison.OrdinalIgnoreCase);
    }
}
