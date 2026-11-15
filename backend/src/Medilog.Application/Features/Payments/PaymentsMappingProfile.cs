using AutoMapper;
using Medilog.Application.Features.Payments.Responses;
using Medilog.Domain.Entities;

namespace Medilog.Application.Features.Payments;

/// <summary>
/// AutoMapper profile for the payments feature. Only a read mapping is needed:
/// client input reaches the domain through the validated request and
/// <see cref="Payment.Create"/>, so identifiers, ownership, status and the paid
/// timestamp are never mapped from client input.
/// </summary>
public sealed class PaymentsMappingProfile : Profile
{
    public PaymentsMappingProfile()
    {
        CreateMap<Payment, PaymentResponse>()
            .ForCtorParam(
                nameof(PaymentResponse.PatientName),
                options => options.MapFrom(source => source.Patient.FullName))
            .ForCtorParam(
                nameof(PaymentResponse.AppointmentDate),
                options => options.MapFrom(source => source.Appointment.AppointmentDate))
            .ForCtorParam(
                nameof(PaymentResponse.PaidAt),
                options => options.MapFrom(source => source.PaidAtUtc));
    }
}
