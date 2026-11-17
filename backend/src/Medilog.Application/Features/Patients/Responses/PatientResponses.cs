using System.Text.Json.Serialization;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Patients.Responses;

/// <summary>
/// A patient as seen by the caller. <see cref="BloodType"/> is a medical field (Q-18): it is mapped
/// only for a doctor and is <c>null</c> (and omitted from the JSON) for a secretary, so a secretary
/// response never contains the property at all.
/// </summary>
public sealed record PatientResponse(
    Guid Id,
    Guid? DoctorId,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Phone,
    string? Address,
    string? EmergencyContact,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BloodType? BloodType,
    DateTimeOffset CreatedAt);
