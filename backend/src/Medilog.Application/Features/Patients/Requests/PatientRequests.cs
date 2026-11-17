using Medilog.Application.Common.Paging;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Patients.Requests;

/// <summary>The editable patient fields shared by create and update, so one rule set validates both.</summary>
public interface IPatientFields
{
    string? FullName { get; }

    DateOnly? DateOfBirth { get; }

    Gender? Gender { get; }

    string? Phone { get; }

    string? Address { get; }

    string? EmergencyContact { get; }

    BloodType? BloodType { get; }
}

/// <summary>
/// Patient creation input. <see cref="DoctorId"/> is only read for a secretary, who must name one of
/// the doctors assigned to them; for a doctor it is ignored and the owner is always the
/// authenticated doctor (AGENTS.md §5). <see cref="BloodType"/> is a medical field: only a doctor may
/// send it (Q-18). Date of birth and gender are nullable so a missing value is a validation error
/// instead of a silent default.
/// </summary>
public sealed record CreatePatientRequest(
    Guid? DoctorId,
    string? FullName,
    DateOnly? DateOfBirth,
    Gender? Gender,
    string? Phone,
    string? Address,
    string? EmergencyContact,
    BloodType? BloodType) : IPatientFields;

/// <summary>
/// Full replacement of the editable fields. The owner, id and creation time cannot be changed. A
/// secretary must leave <see cref="BloodType"/> out; the stored value is never touched by a secretary.
/// </summary>
public sealed record UpdatePatientRequest(
    string? FullName,
    DateOnly? DateOfBirth,
    Gender? Gender,
    string? Phone,
    string? Address,
    string? EmergencyContact,
    BloodType? BloodType) : IPatientFields;

/// <summary>
/// Search (name, phone or exact patient id), optional filters and the shared paging shape.
/// <see cref="DoctorId"/> can only narrow the caller's own scope, never widen it.
/// </summary>
public sealed record ListPatientsQuery(
    string? Search = null,
    Gender? Gender = null,
    Guid? DoctorId = null,
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;

/// <summary>Bounded query behind the patient picker; returns at most <see cref="Limit"/> rows.</summary>
public sealed record PatientPickerQuery(
    string? Search = null,
    Guid? DoctorId = null,
    int Limit = PatientRules.DefaultLookupLimit);
