using System.Diagnostics;
using System.Text.Json;
using Medilog.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Common;

/// <summary>
/// Builds and writes the single API error envelope: RFC 7807
/// <c>application/problem+json</c> plus a stable <c>code</c> and a <c>traceId</c>.
/// Every error path (exception handler, model binding, status-code pages) uses
/// this type so clients see one shape (AGENTS.md §4.4).
/// </summary>
public static class ApiProblem
{
    public const string ContentType = "application/problem+json; charset=utf-8";

    private const string ValidationType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ServerErrorType = "https://tools.ietf.org/html/rfc9110#section-15.6.1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string title,
        string code,
        string? detail = null,
        string? type = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = type ?? TypeFor(status),
            Instance = httpContext.Request.Path,
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = TraceId(httpContext);

        return problem;
    }

    public static ValidationProblemDetails Validation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string[]>? errorCodes,
        string code,
        string title = "One or more validation errors occurred.")
    {
        var problem = new ValidationProblemDetails(ToCamelCaseKeys(errors))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Type = ValidationType,
            Instance = httpContext.Request.Path,
        };

        problem.Extensions["code"] = code;

        if (errorCodes is { Count: > 0 })
        {
            problem.Extensions["errorCodes"] = ToCamelCaseKeys(errorCodes);
        }

        problem.Extensions["traceId"] = TraceId(httpContext);

        return problem;
    }

    /// <summary>Envelope for framework responses that have no body (401/404/405/415/5xx).</summary>
    public static ProblemDetails FromStatusCode(HttpContext httpContext, int status) => status switch
    {
        StatusCodes.Status401Unauthorized => Create(httpContext, status, "Unauthorized.", ErrorCodes.Unauthorized),
        StatusCodes.Status403Forbidden => Create(httpContext, status, "Forbidden.", ErrorCodes.Forbidden),
        StatusCodes.Status404NotFound => Create(httpContext, status, "Resource not found.", ErrorCodes.NotFound),
        StatusCodes.Status405MethodNotAllowed => Create(httpContext, status, "Method not allowed.", ErrorCodes.MethodNotAllowed),
        StatusCodes.Status415UnsupportedMediaType => Create(httpContext, status, "Unsupported media type.", ErrorCodes.UnsupportedMediaType),
        >= 500 => Create(
            httpContext,
            status,
            "An unexpected error occurred.",
            ErrorCodes.ServerError,
            "An unexpected error occurred. Please try again later."),
        _ => Create(httpContext, status, "The request could not be completed.", ErrorCodes.RequestInvalid),
    };

    public static string TraceId(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;

    /// <summary>
    /// Normalizes a model-state or validation key the way the JSON contract names
    /// fields: strips the JSON-path prefix (<c>$.status</c> → <c>status</c>) and
    /// lower-cases the first letter (<c>EndTime</c> → <c>endTime</c>).
    /// </summary>
    public static string NormalizeFieldKey(string key)
    {
        var withoutPath = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;

        return withoutPath.Length == 0 || withoutPath == "$"
            ? "request"
            : char.ToLowerInvariant(withoutPath[0]) + withoutPath[1..];
    }

    public static Dictionary<string, string[]> ToCamelCaseKeys(IReadOnlyDictionary<string, string[]> errors)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, messages) in errors)
        {
            var normalized = NormalizeFieldKey(key);

            result[normalized] = result.TryGetValue(normalized, out var existing)
                ? [.. existing, .. messages]
                : messages;
        }

        return result;
    }

    public static async Task WriteAsync(
        HttpContext httpContext,
        ProblemDetails problem,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = ContentType;

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problem,
            problem.GetType(),
            SerializerOptions,
            cancellationToken);
    }

    /// <summary>Wraps an envelope in an <see cref="IActionResult"/> for MVC result paths.</summary>
    public static IActionResult ToResult(ProblemDetails problem) => new ApiProblemResult(problem);

    private sealed class ApiProblemResult : IActionResult
    {
        private readonly ProblemDetails _problem;

        public ApiProblemResult(ProblemDetails problem)
        {
            _problem = problem;
        }

        public Task ExecuteResultAsync(ActionContext context) =>
            WriteAsync(context.HttpContext, _problem, context.HttpContext.RequestAborted);
    }

    private static string TypeFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status405MethodNotAllowed => "https://tools.ietf.org/html/rfc9110#section-15.5.6",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status415UnsupportedMediaType => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
        >= 500 => ServerErrorType,
        _ => ValidationType,
    };
}
