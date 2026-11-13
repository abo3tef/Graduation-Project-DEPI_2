namespace Medilog.Application.Features.MedicalRecords;

/// <summary>
/// The single place that answers "may this doctor touch this medical record?".
/// </summary>
/// <remarks>
/// <para>
/// The rule recorded for this Cycle (to be confirmed by Q-02) is BRULE-03: a patient belongs to the
/// workspace of the doctor recorded on <c>Patient.DoctorId</c>, and a doctor may only read or write
/// records of patients in their own workspace. A patient with no owning doctor is never accessible
/// until ownership is assigned.
/// </para>
/// <para>
/// A visit is additionally scoped by its own <c>DoctorId</c>, which the service sets from the
/// authenticated doctor, so a visit can never be claimed by another doctor.
/// </para>
/// </remarks>
public static class MedicalRecordAccessPolicy
{
    public static bool CanAccessPatient(Guid? patientOwnerDoctorId, Guid actingDoctorId)
    {
        if (actingDoctorId == Guid.Empty || !patientOwnerDoctorId.HasValue)
        {
            return false;
        }

        return patientOwnerDoctorId.Value == actingDoctorId;
    }
}
