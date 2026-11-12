using Medilog.Domain.Entities;

namespace Medilog.Application.Features.Appointments;

/// <summary>Input limits shared by the appointment validators.</summary>
public static class AppointmentRules
{
    public const int ReasonMaxLength = 1000;

    public const int CancellationReasonMaxLength = Appointment.CancellationReasonMaxLength;
}
