using FluentValidation;

namespace Medilog.Application.Features.Appointments.Lookups;

/// <summary>A doctor the caller may book (administrative data only).</summary>
public sealed record DoctorLookupResponse(Guid Id, string FullName, string? Specialization);

/// <summary>
/// A patient the caller may book. Only the minimum administrative fields are
/// exposed: no date of birth, address, blood type or any medical data.
/// </summary>
public sealed record PatientLookupResponse(Guid Id, string FullName, string? Phone);

/// <summary>Search for the patients of one doctor.</summary>
public sealed record PatientLookupQuery(
    Guid DoctorId,
    string? Search = null,
    int Limit = LookupLimits.DefaultLimit);

public static class LookupLimits
{
    public const int DefaultLimit = 10;

    public const int MaxLimit = 20;

    public const int SearchMaxLength = 100;
}

public sealed class PatientLookupQueryValidator : AbstractValidator<PatientLookupQuery>
{
    public PatientLookupQueryValidator()
    {
        RuleFor(query => query.DoctorId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, LookupLimits.MaxLimit);
        RuleFor(query => query.Search).MaximumLength(LookupLimits.SearchMaxLength);
    }
}
