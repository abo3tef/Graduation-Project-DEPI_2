using AutoMapper;

namespace Medilog.Application.Common.Mapping;

/// <summary>
/// Central AutoMapper profile for the Application layer. Feature mappings are
/// added here or in additional <see cref="Profile"/> classes in this assembly;
/// <c>AddApplication</c> scans the assembly and registers them.
/// </summary>
/// <remarks>
/// Per AGENTS.md §3.3, non-trivial transformations and sensitive fields must be
/// mapped explicitly, and client input must never be mapped directly onto a
/// tracked entity in a way that overwrites IDs, ownership, roles, payment state,
/// audit fields, or protected medical properties.
/// </remarks>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Register explicit maps with CreateMap<TSource, TDestination>() as
        // features are implemented. No implicit convention maps are added here.
    }
}
