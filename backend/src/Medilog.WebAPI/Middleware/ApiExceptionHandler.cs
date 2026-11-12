using System.Diagnostics;
using System.Text.Json;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Middleware;

/// <summary>
/// Single place that turns exceptions into RFC 7807 problem responses:
/// NotFound 404, Forbidden 403, Conflict 409, Domain/Validation 400, anything
/// else a generic 500. Unexpected errors are logged server-side and never leak
/// their message, stack trace or SQL to the client.
/// </summary>
internal sealed class ApiExceptionHandler : IExceptionHandler
{
    private const string ProblemContentType = "application/problem+json";
    private const int ClientClosedRequest = 499;

    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = Map(exception);

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            options: null,
            contentType: ProblemContentType,
            cancellationToken: cancellationToken);

        return true;
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not Found", e.Message),
        ForbiddenException e => Problem(StatusCodes.Status403Forbidden, "Forbidden", e.Message),
        ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflict", e.Message),
        RequestValidationException e => new ValidationProblemDetails(ToCamelCaseKeys(e.Errors))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Detail = null
        },
        DomainException e => Problem(StatusCodes.Status400BadRequest, "Bad Request", e.Message),
        _ => Problem(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred. Please try again later.")
    };

    // Field names are reported the way the JSON contract names them (camelCase).
    private static Dictionary<string, string[]> ToCamelCaseKeys(IReadOnlyDictionary<string, string[]> errors) =>
        errors.ToDictionary(
            error => JsonNamingPolicy.CamelCase.ConvertName(error.Key),
            error => error.Value);

    private static ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };
}
