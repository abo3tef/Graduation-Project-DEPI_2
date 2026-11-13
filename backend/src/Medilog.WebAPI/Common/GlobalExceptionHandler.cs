using FluentValidation;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Common;

/// <summary>
/// Maps application and domain exceptions to the shared RFC 7807 envelope
/// (decisions D-10/D-16, AGENTS.md §4.4).
/// </summary>
/// <remarks>
/// Only application-authored titles and validation messages are returned. The exception message,
/// stack trace, SQL text and any patient data never leave the server (AGENTS.md §4.4, §5).
/// Every response carries a stable <c>code</c> and a <c>traceId</c>; validation responses also
/// carry camelCase <c>errors</c> and <c>errorCodes</c>.
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The client is gone; there is no response to write.
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            if (exception is OperationCanceledException)
            {
                httpContext.Response.StatusCode = ClientClosedRequest;
            }

            return true;
        }

        var problemDetails = CreateProblemDetails(httpContext, exception);

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation(
                "Request {Method} {Path} rejected with status {StatusCode}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                problemDetails.Status);
        }

        await ApiProblem.WriteAsync(httpContext, problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        return exception switch
        {
            RequestValidationException e => ApiProblem.Validation(
                httpContext,
                e.Errors,
                e.FieldErrorCodes,
                e.Code),
            // Safety net: a validator used outside RequestValidation must still
            // produce the same envelope instead of the generic server code.
            ValidationException e => ApiProblem.Validation(
                httpContext,
                GroupByProperty(e),
                ErrorCodesOf(e),
                ErrorCodes.ValidationFailed),
            NotFoundException e => ApiProblem.Create(
                httpContext,
                StatusCodes.Status404NotFound,
                "Resource not found.",
                e.Code,
                e.Message),
            ForbiddenException e => ApiProblem.Create(
                httpContext,
                StatusCodes.Status403Forbidden,
                "Forbidden.",
                e.Code,
                e.Message),
            ConflictException e => ApiProblem.Create(
                httpContext,
                StatusCodes.Status409Conflict,
                "The request conflicts with the current state.",
                e.Code,
                e.Message),
            DomainException e => ApiProblem.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                "The request violates a business rule.",
                e.Code,
                e.Message),
            _ => ApiProblem.Create(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                ErrorCodes.ServerError,
                "An unexpected error occurred. Please try again later."),
        };
    }

    private static Dictionary<string, string[]> GroupByProperty(ValidationException exception) =>
        exception.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

    private static Dictionary<string, string[]> ErrorCodesOf(ValidationException exception) =>
        exception.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorCode).Distinct().ToArray());
}
