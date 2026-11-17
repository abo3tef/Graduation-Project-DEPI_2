using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// Counts every EF Core query compilation on the timeline database, so tests
/// can assert the exact query shape of the timeline use case — in particular
/// that the count-only first pass runs the four source counts and no fetch
/// queries (no "SELECT ... LIMIT 0" work) for a page beyond the last one.
/// </summary>
internal sealed class QueryCountingInterceptor : IQueryExpressionInterceptor
{
    public int QueryCount { get; private set; }

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
    {
        QueryCount++;
        return queryExpression;
    }
}
