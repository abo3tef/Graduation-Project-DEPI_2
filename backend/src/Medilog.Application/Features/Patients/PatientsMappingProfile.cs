using AutoMapper;
using Medilog.Application.Features.Patients.Responses;
using Medilog.Domain.Entities;

namespace Medilog.Application.Features.Patients;

/// <summary>
/// Read mapping only. Client input reaches the entity through <see cref="Patient.Create"/> and
/// <see cref="Patient.UpdateAdministrativeData"/>, never through the mapper, so ids, ownership and
/// medical fields cannot be overwritten by a request (AGENTS.md §3.3).
/// </summary>
public sealed class PatientsMappingProfile : Profile
{
    /// <summary>Mapping context key: <c>true</c> when the caller is a doctor and may see medical fields.</summary>
    public const string IncludeMedicalKey = "patients.include-medical";

    public PatientsMappingProfile()
    {
        CreateMap<Patient, PatientResponse>()
            .ForCtorParam(
                nameof(PatientResponse.BloodType),
                options => options.MapFrom((source, context) =>
                    context.Items.TryGetValue(IncludeMedicalKey, out var include) && include is true
                        ? source.BloodType
                        : null))
            .ForCtorParam(
                nameof(PatientResponse.CreatedAt),
                options => options.MapFrom(source => source.CreatedAtUtc));
    }
}
