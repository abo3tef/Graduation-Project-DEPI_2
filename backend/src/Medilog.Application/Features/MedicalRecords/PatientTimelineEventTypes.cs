namespace Medilog.Application.Features.MedicalRecords;

/// <summary>
/// The timeline event-type contract (F10-C1, decision Q-20): the exact
/// <see cref="PatientTimelineEventResponse.Type"/> values and the accepted
/// <c>eventType</c> filter spellings. VitalSigns events join this list once
/// F9-C1 is merged (F10-C1-T4); labs and documents are excluded by Q-20.
/// </summary>
public static class PatientTimelineEventTypes
{
    public const string Visit = "Visit";

    public const string Diagnosis = "Diagnosis";

    public const string Medication = "Medication";

    public const string MedicalNote = "MedicalNote";
}
