using AutoMapper;
using Medilog.Application.Features.Admin.Responses;
using Medilog.Domain.Entities;

namespace Medilog.Application.Features.Admin;

/// <summary>
/// Read-only mappings for the admin dashboard. Request DTOs are never mapped onto tracked identity
/// entities: status and role changes go through controlled service methods (AGENTS.md §3.3).
/// </summary>
public sealed class AdminMappingProfile : Profile
{
    public AdminMappingProfile()
    {
        CreateMap<AdminUserRecord, AdminUserResponse>()
            .ForCtorParam(nameof(AdminUserResponse.Id), options => options.MapFrom(source => source.Id))
            .ForCtorParam(nameof(AdminUserResponse.Role), options => options.MapFrom(source => source.Role))
            .ForCtorParam(nameof(AdminUserResponse.FullName), options => options.MapFrom(source => source.FullName))
            .ForCtorParam(nameof(AdminUserResponse.Email), options => options.MapFrom(source => source.Email))
            .ForCtorParam(nameof(AdminUserResponse.Phone), options => options.MapFrom(source => source.Phone))
            .ForCtorParam(nameof(AdminUserResponse.Specialization), options => options.MapFrom(source => source.Specialization))
            .ForCtorParam(nameof(AdminUserResponse.Status), options => options.MapFrom(source => source.Status))
            .ForCtorParam(nameof(AdminUserResponse.EmailVerified), options => options.MapFrom(source => source.EmailVerified))
            .ForCtorParam(nameof(AdminUserResponse.CreatedAtUtc), options => options.MapFrom(source => source.CreatedAtUtc));

        CreateMap<Doctor, AdminDoctorResponse>()
            .ForCtorParam(nameof(AdminDoctorResponse.Id), options => options.MapFrom(source => source.Id))
            .ForCtorParam(nameof(AdminDoctorResponse.FullName), options => options.MapFrom(source => source.FullName))
            .ForCtorParam(nameof(AdminDoctorResponse.Email), options => options.MapFrom(source => source.Email));
    }
}
