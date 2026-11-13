using FluentValidation;
using Medilog.Application.Common.Exceptions;

namespace Medilog.Application.Common.Validation;

/// <summary>
/// Runs a FluentValidation validator and turns failures into the shared
/// application validation exception. Field names stay in their CLR form here;
/// the WebAPI layer camelCases them for the JSON contract.
/// </summary>
public static class RequestValidation
{
    public static async Task ValidateAsync<T>(
        IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);

        if (result.IsValid)
        {
            return;
        }

        var grouped = result.Errors.GroupBy(failure => failure.PropertyName).ToArray();

        var errors = grouped.ToDictionary(
            group => group.Key,
            group => group.Select(failure => failure.ErrorMessage).ToArray());

        // Validator codes (e.g. NotEmptyValidator) let clients localize each message.
        var errorCodes = grouped.ToDictionary(
            group => group.Key,
            group => group.Select(failure => failure.ErrorCode).ToArray());

        throw new RequestValidationException(errors, errorCodes);
    }
}
