using System.Text.Json;
using FluentValidation;
using Medilog.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Common;

/// <summary>
/// Maps application exceptions to RFC 7807 <c>ProblemDetails</c> responses (decision D-10/D-16).
/// </summary>
/// <remarks>
/// <para>
/// Only application-authored titles and validation messages are returned. The exception message,
/// stack trace, SQL text and any patient data never leave the server (AGENTS.md §4.4, §5).
/// </para>
/// <para>
/// The runtime type is serialized explicitly so that <see cref="ValidationProblemDetails.Errors"/>
/// is emitted as the root <c>errors</c> member instead of being dropped as a derived-type property.
/// </para>
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const string ContentType = "application/problem+json; charset=utf-8";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

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

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        httpContext.Response.ContentType = ContentType;

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problemDetails,
            problemDetails.GetType(),
            SerializerOptions,
            cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => new ValidationProblemDetails(
                validationException.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Instance = httpContext.Request.Path,
            },
            NotFoundException => CreateProblem(
                httpContext,
                StatusCodes.Status404NotFound,
                "Resource not found.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ForbiddenException => CreateProblem(
                httpContext,
                StatusCodes.Status403Forbidden,
                "Forbidden.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.4"),
            ConflictException => CreateProblem(
                httpContext,
                StatusCodes.Status409Conflict,
                "The request conflicts with the current state.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            _ => CreateProblem(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1"),
        };
    }

    private static ProblemDetails CreateProblem(
        HttpContext httpContext,
        int statusCode,
        string title,
        string type)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Instance = httpContext.Request.Path,
        };
    }
}
